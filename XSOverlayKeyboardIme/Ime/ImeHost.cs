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

        /// <summary>How often the language bar icon is read, so a change reaches the page.</summary>
        private const uint SwitchIconPollMilliseconds = 400;

        private static ImeHost _current;

        private readonly ConcurrentQueue<KeyStroke> _strokes = new ConcurrentQueue<KeyStroke>();
        private readonly Action<string> _onCommitted;
        private readonly Action<CompositionView> _onView;
        private readonly Action<LanguageBarIcon> _onLanguageBarIcon;

        private volatile HostWindow _window;
        private volatile CompositionSession _session;
        private LanguageBarButton _languageBar;
        private ForegroundLease _lease;
        private ImeContext _ime;
        private string _switchIconKey;
        private bool _iconMissing;
        private volatile bool _drainingStrokes;
        private int _finished;

        private ImeHost(
            Action<string> onCommitted,
            Action<CompositionView> onView,
            Action<LanguageBarIcon> onLanguageBarIcon
        )
        {
            _onCommitted = onCommitted;
            _onView = onView;
            _onLanguageBarIcon = onLanguageBarIcon;
        }

        internal static ImeHost Current
        {
            get { return _current; }
        }

        internal static void Start(
            Action<string> onCommitted,
            Action<CompositionView> onView,
            Action<LanguageBarIcon> onLanguageBarIcon
        )
        {
            var host = new ImeHost(onCommitted, onView, onLanguageBarIcon);
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

            // The gate reads a state the host thread owns, so a key that arrives before the
            // queue drains would see a stale answer: a space would go to the page instead of
            // committing. Every key still in flight therefore counts as composing. See ADR 0026.
            bool inFlight = _drainingStrokes || !_strokes.IsEmpty;
            TakeoverGate gate = session.Composing || inFlight ? TakeoverGate.Composing : TakeoverGate.Idle;

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

        /// <summary>
        /// Asks the IME to switch through its own language bar button. Called on Unity's
        /// main thread; the press itself happens on the host thread. See ADR 0028.
        /// </summary>
        internal void RequestSwitch()
        {
            HostWindow window = _window;
            if (window != null)
            {
                window.Post(HostWindow.SwitchMessage);
            }
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

                string imeName = ImeName.Read();
                HostLog.Info("ime name: " + (imeName.Length == 0 ? "unreadable" : imeName));

                var feeder = new KeyFeeder(_window, _lease);
                var session = new CompositionSession(_ime, _window, _lease, feeder, imeName, _onCommitted, _onView);
                session.Start();
                _session = session;

                // The IME adds its language bar item when it first activates, which happens
                // after the host starts, so the button is looked up again on every poll.
                _languageBar = LanguageBarButton.Open();
                PublishSwitchIcon();
                _window.StartTimer(SwitchIconPollMilliseconds);

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
                return;
            }

            if (message == HostWindow.PollMessage)
            {
                if (_languageBar == null)
                {
                    _languageBar = LanguageBarButton.Open();
                }

                PublishSwitchIcon();
                return;
            }

            if (message == HostWindow.SwitchMessage)
            {
                if (_languageBar != null && _languageBar.Click())
                {
                    PublishSwitchIcon();
                }
            }
        }

        /// <summary>Shows the page the current icon, and only when the picture changed.</summary>
        private void PublishSwitchIcon()
        {
            LanguageBarButton languageBar = _languageBar;
            if (languageBar == null)
            {
                return;
            }

            LanguageBarIcon icon = languageBar.ReadIcon();
            if (icon == null)
            {
                if (!_iconMissing)
                {
                    _iconMissing = true;
                    HostLog.Info("the language bar item draws no icon; the switch shows its fallback");
                }

                return;
            }

            if (icon.Key == _switchIconKey)
            {
                return;
            }

            _switchIconKey = icon.Key;
            _onLanguageBarIcon(icon);
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

            if (_languageBar != null)
            {
                _languageBar.Dispose();
                _languageBar = null;
            }

            if (_ime != null)
            {
                _ime.Dispose();
                _ime = null;
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
