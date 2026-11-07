using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// Owns the STA thread, the hidden window and the IME context, and hands page keys to
    /// the composition session. See ADR 0005, ADR 0014, ADR 0022 and ADR 0035.
    /// </summary>
    internal sealed class ImeHost
    {
        /// <summary>XSOverlay finishes starting after the plugin, and steals the foreground while it does.</summary>
        private const int StartupSettleMilliseconds = 8000;

        /// <summary>How long a language switch needs before the new IME can be read.</summary>
        private const uint SwitchSettleMilliseconds = 400;

        private static ImeHost _current;

        private readonly ConcurrentQueue<KeyStroke> _strokes = new ConcurrentQueue<KeyStroke>();
        private readonly Action<string> _onCommitted;
        private readonly Action<CompositionView> _onView;
        private readonly Action<LanguageBarIcon> _onLanguageBarIcon;
        private readonly Action<ComposerState> _onComposerState;

        private volatile HostWindow _window;
        private volatile CompositionSession _session;
        private LanguageBarButton _languageBar;
        private ForegroundLease _lease;
        private ImeContext _ime;
        private ImeProfiles _profiles;
        private string _switchIconKey;
        private string _imeName = string.Empty;
        private bool _iconMissing;
        private volatile bool _composerActive;
        private volatile bool _drainingStrokes;
        private int _finished;

        private ImeHost(
            Action<string> onCommitted,
            Action<CompositionView> onView,
            Action<LanguageBarIcon> onLanguageBarIcon,
            Action<ComposerState> onComposerState
        )
        {
            _onCommitted = onCommitted;
            _onView = onView;
            _onLanguageBarIcon = onLanguageBarIcon;
            _onComposerState = onComposerState;
        }

        internal static ImeHost Current
        {
            get { return _current; }
        }

        internal static void Start(
            Action<string> onCommitted,
            Action<CompositionView> onView,
            Action<LanguageBarIcon> onLanguageBarIcon,
            Action<ComposerState> onComposerState
        )
        {
            var host = new ImeHost(onCommitted, onView, onLanguageBarIcon, onComposerState);
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
            if (KeyStrokeReader.Read(key, gate, strokes) == KeyOwnership.Page)
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

        /// <summary>Called on Unity's main thread; the page reads it through the host. See ADR 0035.</summary>
        internal void SetComposerActive(bool active)
        {
            _composerActive = active;
            HostWindow window = _window;
            if (window != null)
            {
                window.Post(HostWindow.ComposerMessage);
            }
        }

        /// <summary>Cancels the composition the current host holds, if there is one. See ADR 0027.</summary>
        internal static void CancelCurrent()
        {
            ImeHost host = _current;
            if (host != null)
            {
                host.RequestCancel();
            }
        }

        /// <summary>Hides what belongs to the composer, for a keyboard that went away. See ADR 0035.</summary>
        internal static void DeactivateComposer()
        {
            ImeHost host = _current;
            if (host != null)
            {
                host.SetComposerActive(false);
            }
        }

        /// <summary>
        /// Asks the IME to cancel the composition, the way Escape does. Called on Unity's
        /// main thread; the key is fed on the host thread. See ADR 0027.
        /// </summary>
        internal void RequestCancel()
        {
            CompositionSession session = _session;
            HostWindow window = _window;
            if (session == null || window == null)
            {
                return;
            }

            _strokes.Enqueue(KeyStroke.FromKey(VirtualKey.Escape, false));
            window.Post(HostWindow.StrokeMessage);
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

                _profiles = ImeProfiles.Open();

                var feeder = new KeyFeeder(_window, _lease);
                var session = new CompositionSession(_ime, _window, _lease, feeder, _onCommitted, _onView);
                session.Start();
                _session = session;

                if (_composerActive)
                {
                    RefreshComposer();
                }

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

            if (message == HostWindow.ComposerMessage)
            {
                RefreshComposer();
                return;
            }

            if (message == HostWindow.LanguageMessage)
            {
                LanguageChanged();
                return;
            }

            if (message == HostWindow.SwitchMessage)
            {
                PressSwitch();
            }

            if (message == HostWindow.SwitchSettledMessage)
            {
                OnSwitchSettled();
            }
        }

        /// <summary>Reads the IME name again while the composer holds the focus. See ADR 0035.</summary>
        private void RefreshComposer()
        {
            string name = _composerActive ? ReadImeName() : string.Empty;
            if (name != _imeName)
            {
                _imeName = name;
                if (_composerActive)
                {
                    HostLog.Info("the IME is now '" + (name.Length == 0 ? "unreadable" : name) + "'");
                }
            }

            if (_composerActive)
            {
                PublishSwitchIcon();
                _onComposerState(new ComposerState(true, name));
                return;
            }

            _onComposerState(ComposerState.Hidden);
        }

        /// <summary>This thread's language changed, so the IME and its bar item are new.</summary>
        private void LanguageChanged()
        {
            if (!_composerActive)
            {
                return;
            }

            ReopenLanguageBar();
            RefreshComposer();
        }

        /// <summary>Reads the new IME once the switch we asked for has settled. See ADR 0036.</summary>
        private void ScheduleSwitchSettled()
        {
            HostWindow window = _window;
            if (window == null)
            {
                return;
            }

            Win32Native.SetTimer(window.Handle, HostWindow.SwitchSettleTimerId, SwitchSettleMilliseconds, IntPtr.Zero);
        }

        private void OnSwitchSettled()
        {
            HostWindow window = _window;
            if (window == null)
            {
                return;
            }

            Win32Native.KillTimer(window.Handle, HostWindow.SwitchSettleTimerId);
            LanguageChanged();
        }

        private void PressSwitch()
        {
            if (_languageBar == null)
            {
                ReopenLanguageBar();
            }

            if (_languageBar != null && _languageBar.Click())
            {
                PublishSwitchIcon();
            }
        }

        /// <summary>Looks the language bar item up again, after the IME may have replaced it. See ADR 0028.</summary>
        private void ReopenLanguageBar()
        {
            if (_languageBar != null)
            {
                _languageBar.Dispose();
                _languageBar = null;
            }

            _languageBar = LanguageBarButton.Open();
            _switchIconKey = null;
            _iconMissing = false;
        }

        /// <summary>The IME name this thread would report, or empty when unreadable.</summary>
        private string ReadImeName()
        {
            ImeProfiles profiles = _profiles;
            return profiles == null ? string.Empty : profiles.ReadName();
        }

        /// <summary>Shows the page the current icon, and only when the picture changed. See ADR 0028.</summary>
        private void PublishSwitchIcon()
        {
            if (!_composerActive)
            {
                return;
            }

            if (_languageBar == null)
            {
                ReopenLanguageBar();
                if (_languageBar == null)
                {
                    return;
                }
            }

            LanguageBarIcon icon = _languageBar.ReadIcon();
            if (icon == null)
            {
                // The new IME has no bar item, so the page must stop drawing the old one.
                if (!_iconMissing)
                {
                    _iconMissing = true;
                    _switchIconKey = null;
                    HostLog.Info("the language bar item draws no icon; the switch shows its fallback");
                    _onLanguageBarIcon(null);
                }

                return;
            }

            _iconMissing = false;
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

                    // We asked the system to switch, so nothing else will tell this host.
                    // The new IME is only readable once the switch settles. See ADR 0036.
                    if (stroke.Kind == KeyStrokeKind.System)
                    {
                        ScheduleSwitchSettled();
                    }
                }

                // A fed Shift is the IME's own switch key, so the icon is read again here.
                PublishSwitchIcon();
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

            if (_profiles != null)
            {
                _profiles.Dispose();
                _profiles = null;
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
