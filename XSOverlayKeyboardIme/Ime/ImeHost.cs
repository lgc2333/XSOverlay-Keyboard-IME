using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// Owns the STA thread, the hidden window and the IME context, and hands page keys to
    /// the composition session. See ADR 0005, ADR 0014 and ADR 0022.
    /// </summary>
    internal sealed class ImeHost
    {
        /// <summary>XSOverlay finishes starting after the plugin, and steals the foreground while it does.</summary>
        private const int StartupSettleMilliseconds = 8000;

        private static ImeHost _current;

        private readonly ConcurrentQueue<KeyStroke> _strokes = new ConcurrentQueue<KeyStroke>();
        private readonly Action<string> _onCommitted;
        private readonly Action<CompositionView> _onView;

        private volatile HostWindow _window;
        private volatile CompositionSession _session;
        private ForegroundLease _lease;
        private ImeContext _ime;
        private ImeCompartments _compartments;
        private bool _drainingStrokes;
        private int _finished;

        private ImeHost(Action<string> onCommitted, Action<CompositionView> onView)
        {
            _onCommitted = onCommitted;
            _onView = onView;
        }

        internal static ImeHost Current
        {
            get { return _current; }
        }

        internal static void Start(Action<string> onCommitted, Action<CompositionView> onView)
        {
            var host = new ImeHost(onCommitted, onView);
            _current = host;

            var thread = new Thread(host.Run) { IsBackground = true, Name = "XSOverlayKeyboardIme" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        /// <summary>
        /// Takes a page key when this plugin owns it. Called on Unity's main thread; a
        /// true answer suppresses the page's own handling.
        /// </summary>
        internal bool TryAcceptKey(PageKey key)
        {
            CompositionSession session = _session;
            HostWindow window = _window;
            if (session == null || window == null)
            {
                return false;
            }

            TakeoverGate gate =
                session.Composing ? TakeoverGate.Composing
                : session.Open ? TakeoverGate.Session
                : TakeoverGate.Idle;

            var strokes = new List<KeyStroke>(4);
            if (!KeyStrokeReader.TryRead(key, gate, strokes))
            {
                return false;
            }

            foreach (KeyStroke stroke in strokes)
            {
                _strokes.Enqueue(stroke);
            }

            window.Post(HostWindow.StrokeMessage);
            return true;
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
                _lease = new ForegroundLease(_window.Handle);

                try
                {
                    _ime = new ImeContext(_window.Handle);
                }
                catch (Exception ex)
                {
                    HostLog.Error("the IME context is unavailable: " + ex.GetType().Name + " " + ex.Message);
                    return;
                }

                _compartments = ImeCompartments.Open();

                string imeName = ImeName.Read();
                HostLog.Info("ime name: " + (imeName.Length == 0 ? "unreadable" : imeName));

                var feeder = new KeyFeeder(_window, _lease);
                var session = new CompositionSession(
                    _ime,
                    _window,
                    _lease,
                    feeder,
                    _compartments,
                    imeName,
                    _onCommitted,
                    _onView
                );
                session.Start();
                _session = session;

                HostLog.Info("take-over ready; keys are taken from the keyboard composer");
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

        private void OnHostMessage(uint message)
        {
            if (message == HostWindow.StrokeMessage)
            {
                DrainStrokes();
            }
        }

        /// <summary>
        /// Feeds the queued keys. Waiting for the IME pumps this same queue, so a nested
        /// call is possible and must not feed one key twice.
        /// </summary>
        private void DrainStrokes()
        {
            if (_drainingStrokes)
            {
                return;
            }

            _drainingStrokes = true;
            try
            {
                CompositionSession session = _session;
                if (session == null)
                {
                    return;
                }

                while (_strokes.TryDequeue(out KeyStroke stroke))
                {
                    session.Send(stroke);
                }
            }
            finally
            {
                _drainingStrokes = false;
            }
        }

        private void Finish()
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0)
            {
                return;
            }

            if (_ime != null)
            {
                _ime.Dispose();
                _ime = null;
            }

            if (_compartments != null)
            {
                _compartments.Dispose();
                _compartments = null;
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
