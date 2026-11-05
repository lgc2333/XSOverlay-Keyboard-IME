using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Vuplex.WebView;
using XSOverlay;
using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Game;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Page;
using XSOverlayKeyboardIme.Takeover;
#if IME_SELF_CHECK
using XSOverlayKeyboardIme.SelfCheck;
#endif

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Owns the STA thread, the hidden host window and the TSF bridge. It runs the live
    /// take-over, or the diagnostic self-check in a build that includes it. See ADR 0006.
    /// </summary>
    internal sealed partial class CompositionHost
    {
        /// <summary>XSOverlay finishes starting after the plugin, and steals the foreground while it does.</summary>
        private const int StartupSettleMilliseconds = 8000;

        private static volatile CompositionHost _current;

        private readonly ConcurrentQueue<KeyStroke> _strokes = new ConcurrentQueue<KeyStroke>();
        private readonly KeyboardPage _page;

        private HostWindow _window;
        private ForegroundLease _lease;
        private ThreadManagerBridge _bridge;
        private CompositionService _service;
        private IWebView _target;
        private bool _pageAttached;
        private int _finished;
        private static long _lastRefusalTicks;

        internal CompositionHost()
        {
            _page = new KeyboardPage();
        }

        internal static void Start()
        {
            Launch(new CompositionHost());
        }

        private static void Launch(CompositionHost host)
        {
            _current = host;
            var thread = new Thread(host.Run) { IsBackground = true, Name = "XSOverlayKeyboardIme" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        internal static CompositionHost Current
        {
            get { return _current; }
        }

        /// <summary>
        /// Takes a page key event when this plugin owns it. Called on Unity's main thread;
        /// a true answer suppresses the page's own handling.
        /// </summary>
        internal bool TryAcceptKey(XSOverlay.Websockets.API.Objects.KeyboardEvent keyEvent)
        {
            CompositionService service = _service;
            if (service == null || _window == null)
            {
                return false;
            }

            if (!KeyboardOverlayScope.TryGetFocusedOverlay(out KeyboardInputFocusManager focus))
            {
                LogRefusal("no focus on the keyboard overlay: " + KeyboardOverlayScope.Describe());
                return false;
            }

            bool composing = service.Composing;
            var strokes = new List<KeyStroke>(4);
            if (!KeyStrokeReader.TryRead(keyEvent, composing, strokes))
            {
                LogRefusal("the key was not taken: " + DescribeKey(keyEvent));
                return false;
            }

            _target = focus.TargetWebView;
            if (!_pageAttached && _target != null)
            {
                // ADR 0004: the page gets the script when the take-over first needs it.
                _pageAttached = true;
                IWebView page = _target;
                MainThreadDispatcher.Post(() => _page.Attach(page));
            }

            foreach (KeyStroke stroke in strokes)
            {
                _strokes.Enqueue(stroke);
            }

            _window.Post(HostWindow.StrokeMessage);
            return true;
        }

        /// <summary>Explains a refusal, at most once every two seconds.</summary>
        private static void LogRefusal(string reason)
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - Interlocked.Read(ref _lastRefusalTicks) < TimeSpan.TicksPerSecond * 2)
            {
                return;
            }

            Interlocked.Exchange(ref _lastRefusalTicks, now);
            HostLog.Info("key refused: " + reason);
        }

        private static string DescribeKey(XSOverlay.Websockets.API.Objects.KeyboardEvent keyEvent)
        {
            if (keyEvent == null)
            {
                return "(null)";
            }

            string values = keyEvent.value == null ? "(null)" : string.Join(",", keyEvent.value);
            return "style="
                + keyEvent.keyPressStyle
                + " values=["
                + values
                + "] web="
                + (keyEvent.webValue ?? "(null)");
        }

        private void Run()
        {
            try
            {
                Win32Native.SetErrorMode(Win32Native.SemFailCriticalErrors | Win32Native.SemNoOpenFileErrorBox);
                int hr = Win32Native.CoInitializeEx(IntPtr.Zero, Win32Native.CoInitApartmentThreaded);
                if (hr < 0)
                {
                    HostLog.Error("CoInitializeEx failed 0x" + hr.ToString("x"));
                    return;
                }

                Thread.Sleep(StartupSettleMilliseconds);
                HostLog.Info(
                    "foreground before the lease: " + ForegroundLease.Describe(Win32Native.GetForegroundWindow())
                );
                _window = HostWindow.Create(OnHostMessage);
                _lease = new ForegroundLease(_window.Handle, _window.Editor);
                var feeder = new KeyFeeder(_window, _lease);

#if IME_SELF_CHECK
                bool started = TryStartSelfCheck(feeder) || StartTakeOver(feeder);
#else
                bool started = StartTakeOver(feeder);
#endif
                if (!started)
                {
                    return;
                }

                _window.PumpMessages();
            }
            catch (Exception ex)
            {
                HostLog.Error("host thread failed " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                Finish();
                Win32Native.CoUninitialize();
            }
        }

        private bool StartTakeOver(KeyFeeder feeder)
        {
            string imeName = ImeProfile.ReadName();
            HostLog.Info("ime name: " + (imeName.Length == 0 ? "unreadable" : imeName));

            CompositionService service = new CompositionService(_window, _lease, feeder, OnCommitted, OnView, imeName);
            _bridge = ThreadManagerBridge.Open(service);
            if (_bridge == null)
            {
                return false;
            }

            service.Attach(_bridge);
            _service = service;
            _window.PumpPending();
            HostLog.Info("take-over ready; keys are taken from the keyboard composer");
            return true;
        }

        private void OnHostMessage(uint message)
        {
            if (message == HostWindow.StrokeMessage)
            {
                DrainStrokes();
                return;
            }

#if IME_SELF_CHECK
            if (HandleSelfCheckMessage(message))
            {
                return;
            }
#endif

            if (_service != null)
            {
                _service.HandleMessage(message);
            }
        }

        private void DrainStrokes()
        {
            CompositionService service = _service;
            if (service == null)
            {
                return;
            }

            while (_strokes.TryDequeue(out KeyStroke stroke))
            {
                service.Send(stroke);
            }
        }

        /// <summary>Hands the committed text to Unity's main thread, where the page lives.</summary>
        private void OnCommitted(string text)
        {
            IWebView target = _target;
            MainThreadDispatcher.Post(() => _page.WriteBack(target, text));
        }

        /// <summary>Hands the composition to the page, which shows it under the keys.</summary>
        private void OnView(CompositionView view)
        {
            IWebView target = _target;
            if (target == null)
            {
                return;
            }

            MainThreadDispatcher.Post(() => _page.Show(target, view));
        }

        private void Finish()
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0)
            {
                return;
            }

            if (_bridge != null)
            {
                _bridge.Dispose();
            }

            if (_lease != null)
            {
                _lease.Restore();
                _lease.EndAttach();
            }

            if (_window != null)
            {
                _window.Close();
            }

            Win32Native.PostQuitMessage(0);
        }
    }
}
