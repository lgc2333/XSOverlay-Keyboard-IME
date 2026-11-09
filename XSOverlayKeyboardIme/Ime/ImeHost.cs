using System;
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
    /// the composition session. See ADR 0005, ADR 0014, ADR 0022, ADR 0035, ADR 0038
    /// and ADR 0039.
    /// </summary>
    internal sealed class ImeHost
    {
        private static ImeHost _current;

        private readonly Queue<(KeyStroke Stroke, FocusTicket Owner)> _strokes = new Queue<(KeyStroke, FocusTicket)>();
        private readonly object _strokeLock = new object();
        private readonly FocusAdmission _focus = new FocusAdmission();
        private readonly Dictionary<VirtualKey, FocusTicket> _modifierOwners =
            new Dictionary<VirtualKey, FocusTicket>();
        internal Action<string, FocusTicket> FocusChanged;

        private readonly Action<string, Action<bool>> _onCommitted;
        internal Action<string, bool, Action<bool>> NativeKey;
        private readonly PageOperationQueue _pageOperations;
        private readonly Dictionary<int, bool> _browserKeys = new Dictionary<int, bool>();
        private readonly Action<CompositionView> _onView;
        private readonly Action<InputModeView> _onLanguageBarIcon;
        private readonly Action<ComposerState> _onComposerState;

        private volatile HostWindow _window;
        private volatile CompositionSession _session;
        private InputModeButton _modeButton;
        private ForegroundLease _lease;
        private ImeContext _ime;
        private ImeProfiles _profiles;
        private ImeProfileNotifications _profileNotifications;
        private LanguageProfile? _switchProfile;
        private string _imeName = string.Empty;
        private volatile bool _composerActive;
        private volatile bool _drainingStrokes;
        private readonly Dictionary<FocusTicket, int> _pendingCompositionKeys = new Dictionary<FocusTicket, int>();
        private int _finished;
        private bool _acquiringFocus;
        private bool _endAfterAcquire;
        private FocusTicket? _deferredAcquire;

        private ImeHost(
            Action<string, Action<bool>> onCommitted,
            Action<CompositionView> onView,
            Action<InputModeView> onLanguageBarIcon,
            Action<ComposerState> onComposerState
        )
        {
            _onCommitted = onCommitted;
            _onView = onView;
            _onLanguageBarIcon = onLanguageBarIcon;
            _onComposerState = onComposerState;
            _pageOperations = new PageOperationQueue(
                action => _window?.Post(action),
                RevokeFocus,
                () => _window?.Post(HostWindow.StrokeMessage)
            );
        }

        internal static ImeHost Current
        {
            get { return _current; }
        }

        internal bool Ready => _session != null && _window != null;

        internal bool TryPostModifier(KeyStroke stroke)
        {
            if (!Ready)
                return false;
            lock (_strokeLock)
            {
                FocusTicket? owner = _focus.Current;
                if (stroke.Kind == KeyStrokeKind.ModifierUp)
                {
                    if (!_modifierOwners.TryGetValue(stroke.Key, out FocusTicket down))
                        return false;
                    owner = down;
                    _modifierOwners.Remove(stroke.Key);
                }
                if (!owner.HasValue)
                    return false;
                if (stroke.Kind == KeyStrokeKind.ModifierDown)
                    _modifierOwners[stroke.Key] = owner.Value;
                Enqueue(stroke, owner.Value);
            }
            _window.Post(HostWindow.StrokeMessage);
            return true;
        }

        internal static void Start(
            Action<string, Action<bool>> onCommitted,
            Action<CompositionView> onView,
            Action<InputModeView> onLanguageBarIcon,
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
        internal bool TryAcceptKey(
            PageKey key,
            Action consumeSticky,
            FocusTicket? source = null,
            bool includePageKeys = false
        )
        {
            CompositionSession session = _session;
            HostWindow window = _window;
            if (session == null || window == null)
            {
                return false;
            }
            FocusTicket? owner = source ?? _focus.Current;
            if (!owner.HasValue || !_focus.Allows(owner.Value))
                return false;

            // A queued ordinary key may start composition. Modifier edges do not. See ADR 0040.
            bool inFlight;
            lock (_strokeLock)
                inFlight = _pendingCompositionKeys.TryGetValue(owner.Value, out int count) && count > 0;
            TakeoverGate gate = session.Composing || inFlight ? TakeoverGate.Composing : TakeoverGate.Idle;

            var strokes = new List<KeyStroke>(4);
            if (KeyStrokeReader.Read(key, gate, strokes, includePageKeys) == KeyOwnership.Page)
            {
                window.Post(HostWindow.ComposerMessage);
                return false;
            }

            lock (_strokeLock)
            {
                foreach (KeyStroke stroke in strokes)
                {
                    Enqueue(stroke, owner.Value);
                }

                // The drain cannot see the ordinary key until its sticky releases are queued.
                consumeSticky();
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
            FocusTicket? owner = _focus.Current;
            if (owner.HasValue)
                _window?.Post(() => PressSwitch(owner.Value));
        }

        /// <summary>Called on Unity's main thread; the page reads it through the host. See ADR 0035.</summary>
        internal void SetComposerActive(bool active)
        {
            // Status reports and automatic focus never grant admission. See ADR 0060.
            if (!active)
                RevokeFocus();
        }

        internal void BindComposerDocument(string document)
        {
            FocusTicket clear = _focus.Bind(document);
            _window?.Post(() => ClearFocus(clear));
        }

        internal void ForgetComposerDocument()
        {
            _focus.Detach();
            _window?.Post(() =>
            {
                if (_focus.Detached)
                    EndFocus();
            });
        }

        internal void HandleFocusMessage(FocusMessage message)
        {
            FocusTicket ticket = message.Ticket;
            switch (message.Kind)
            {
                case "cleared":
                    if (_focus.Acknowledge(ticket))
                        FocusChanged?.Invoke("open", ticket);
                    break;
                case "request":
                    if (_focus.TryAcquire(ticket))
                        _window?.Post(() => AcquireFocus(ticket));
                    break;
                case "cancel-focus":
                    if (_focus.Cancel(ticket, out FocusTicket clear))
                        _window?.Post(() => ClearFocus(clear));
                    break;
                case "profile":
                    _window?.Post(() =>
                    {
                        if (CanSend(ticket))
                            SwitchProfile(message.Index, ticket);
                    });
                    break;
                case "switch":
                    _window?.Post(() => PressSwitch(ticket));
                    break;
                case "cancel-composition":
                    QueueCancel(ticket);
                    break;
            }
        }

        private void AcquireFocus(FocusTicket ticket)
        {
            if (!_focus.IsAcquiring(ticket))
                return;
            if (_acquiringFocus)
            {
                _deferredAcquire = ticket;
                return;
            }
            _acquiringFocus = true;
            try
            {
                if (_lease.Acquire(() => _focus.IsAcquiring(ticket)) && _focus.Complete(ticket))
                {
                    _composerActive = true;
                    FocusChanged?.Invoke("held", ticket);
                    RefreshComposer();
                }
                else if (_focus.IsAcquiring(ticket))
                    RevokeFocus();
            }
            catch (Exception exception)
            {
                HostLog.Warn("focus acquisition failed: " + exception.Message);
                if (_focus.IsAcquiring(ticket))
                    RevokeFocus();
            }
            finally
            {
                _acquiringFocus = false;
                if (_endAfterAcquire)
                {
                    _endAfterAcquire = false;
                    _lease.Restore();
                }
                FocusTicket? pending = _deferredAcquire;
                _deferredAcquire = null;
                if (pending.HasValue)
                    _window.Post(() => AcquireFocus(pending.Value));
            }
        }

        private bool CanSend(FocusTicket ticket)
        {
            if (!_focus.Allows(ticket))
                return false;
            if (_lease.Ensure())
                return _focus.Allows(ticket);
            RevokeFocus();
            return false;
        }

        private void RevokeFocus()
        {
            if (_focus.Revoke(out FocusTicket clear))
            {
                // Admission is already closed. Native loss calls this on the original STA.
                _window?.Post(() => ClearFocus(clear));
            }
        }

        private void ClearFocus(FocusTicket clear)
        {
            if (!_focus.IsClearing(clear))
                return;
            EndFocus();
            if (_focus.IsClearing(clear))
                FocusChanged?.Invoke("clear", clear);
        }

        private void EndFocus()
        {
            _pageOperations.Cancel();
            _browserKeys.Clear();
            _composerActive = false;
            if (_acquiringFocus)
                _endAfterAcquire = true;
            else
                _lease?.Restore();
            RefreshComposer();
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

        /// <summary>Asks for the IME at this index. Called on Unity's main thread. See ADR 0039.</summary>
        internal void RequestProfile(int index)
        {
            HostWindow window = _window;
            if (window == null)
            {
                return;
            }

            FocusTicket? owner = _focus.Current;
            if (owner.HasValue)
                window.Post(() =>
                {
                    if (CanSend(owner.Value))
                        SwitchProfile(index, owner.Value);
                });
        }

        /// <summary>
        /// Asks the IME to cancel the composition, the way Escape does. Called on Unity's
        /// main thread; the key is fed on the host thread. See ADR 0027.
        /// </summary>
        internal void RequestCancel()
        {
            FocusTicket? owner = _focus.Current;
            if (owner.HasValue)
                QueueCancel(owner.Value);
        }

        private void QueueCancel(FocusTicket owner)
        {
            CompositionSession session = _session;
            HostWindow window = _window;
            if (session == null || window == null)
            {
                return;
            }

            lock (_strokeLock)
            {
                if (!_focus.Allows(owner))
                    return;
                Enqueue(KeyStroke.FromKey(VirtualKey.Escape, false), owner);
            }
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
                _profileNotifications = ImeProfileNotifications.Open(() => _window.Post(HostWindow.LanguageMessage));

                var feeder = new KeyFeeder(_window);
                _window.ProcessKeyboard = ProcessKeyboard;
                _window.KeyboardPaused = () => _pageOperations.Pending;
                _window.Typed = CommitText;
                var session = new CompositionSession(_ime, _window, _lease, feeder, CommitText, _onView);
                session.Start();
                _session = session;
                _modeButton = new InputModeButton(_window, session, () => _profiles.ReadActive(), _onLanguageBarIcon);
                FocusTicket? initialClear = _focus.PendingClear;
                if (initialClear.HasValue)
                    ClearFocus(initialClear.Value);

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
            switch (message)
            {
                case HostWindow.StrokeMessage:
                    DrainStrokes();
                    break;
                case HostWindow.ComposerMessage:
                    RefreshComposer();
                    break;
                case HostWindow.LanguageMessage:
                    LanguageChanged();
                    break;
                case HostWindow.SwitchMessage:
                    break;
                case HostWindow.ModeCompleteMessage:
                    _modeButton?.Complete();
                    break;
                case HostWindow.ProfileMessage:
                    break;
                case HostWindow.FocusLostMessage:
                    RevokeFocus();
                    break;
            }
        }

        /// <summary>Reads the IME name again while the composer holds the focus. See ADR 0035.</summary>
        private void RefreshComposer()
        {
            FocusTicket? owner = _focus.Current;
            LanguageProfile? profile = _composerActive ? _profiles?.ReadActive() : null;
            string name = _composerActive ? _profiles?.Describe(profile) ?? string.Empty : string.Empty;
            if (owner.HasValue && !_focus.Allows(owner.Value))
                return;
            bool nameChanged = name != _imeName;
            if (nameChanged)
            {
                _imeName = name;
                if (_composerActive)
                {
                    HostLog.Info("the IME is now '" + (name.Length == 0 ? "unreadable" : name) + "'");
                }
            }

            if (_composerActive)
            {
                // The reader's list is refilled by its next read, so the page gets a copy.
                IReadOnlyList<ImeChoice> listed =
                    _profiles == null ? Array.Empty<ImeChoice>() : _profiles.ListChoices();
                if (!owner.HasValue || !_focus.Allows(owner.Value))
                    return;
                var choices = new ImeChoice[listed.Count];
                int activeProfile = -1;
                for (int index = 0; index < listed.Count; index++)
                {
                    choices[index] = listed[index];
                    if (
                        activeProfile < 0
                        && name.Length > 0
                        && profile.HasValue
                        && listed[index].Profile.Equals(profile.Value)
                    )
                    {
                        activeProfile = index;
                    }
                }

                if (nameChanged || !Nullable.Equals(profile, _switchProfile))
                {
                    _switchProfile = profile;
                    ClearLanguageBar();
                }

                PublishSwitchIcon();
                if (!_focus.Allows(owner.Value))
                    return;
                _onComposerState(new ComposerState(true, name, choices, activeProfile));
                return;
            }

            _switchProfile = null;
            ClearLanguageBar();
            if (!_focus.Current.HasValue)
                _onComposerState(ComposerState.Hidden);
        }

        /// <summary>Switches this thread's IME. Called on the host thread. See ADR 0039.</summary>
        private void SwitchProfile(int index, FocusTicket owner)
        {
            ImeProfiles profiles = _profiles;
            CompositionSession session = _session;
            if (
                !_composerActive
                || profiles == null
                || session == null
                || !profiles.TryGetChoice(index, out LanguageProfile profile)
                || !session.ActivateProfile(profiles, profile, () => CanSend(owner))
            )
            {
                // Push the state back, so the page does not keep a checkmark that lies.
                HostLog.Warn("the profile " + index + " was not activated");
                RefreshComposer();
                return;
            }

            HostLog.Info("the page switched to profile " + index);
            ClearLanguageBar();
            RefreshComposer();
        }

        /// <summary>This thread's language changed, so the IME and its bar item are new.</summary>
        private void LanguageChanged()
        {
            if (!_composerActive)
            {
                return;
            }

            ClearLanguageBar();
            RefreshComposer();
        }

        /// <summary>A request for another IME. See ADR 0039.</summary>
        private void PressSwitch(FocusTicket owner)
        {
            if (CanSend(owner))
                _modeButton?.Click(() => CanSend(owner));
        }

        private void ClearLanguageBar() => _modeButton?.Reset();

        private void PublishSwitchIcon() => _modeButton?.Refresh(_switchProfile);

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

                while (!_pageOperations.Pending && TryDequeue(out var queued))
                {
                    KeyStroke stroke = queued.Stroke;
                    try
                    {
                        if (stroke.Kind == KeyStrokeKind.ModifierUp || CanSend(queued.Owner))
                            if (
                                !session.Send(stroke, queued.Owner, () => CanSend(queued.Owner))
                                && _focus.Allows(queued.Owner)
                            )
                                RevokeFocus();
                    }
                    catch (Exception exception)
                    {
                        HostLog.Warn("queued key failed: " + exception.Message);
                        if (_focus.Allows(queued.Owner))
                            RevokeFocus();
                    }
                    finally
                    {
                        if (stroke.CountsAsComposition)
                            lock (_strokeLock)
                            {
                                int remaining = _pendingCompositionKeys[queued.Owner] - 1;
                                if (remaining == 0)
                                    _pendingCompositionKeys.Remove(queued.Owner);
                                else
                                    _pendingCompositionKeys[queued.Owner] = remaining;
                            }
                    }
                }

                RefreshComposer();
            }
            finally
            {
                _drainingStrokes = false;
            }
        }

        private void CommitText(string text) => QueuePageOperation(done => _onCommitted(text, done));

        private void QueuePageOperation(Action<Action<bool>> operation)
        {
            _pageOperations.Enqueue(operation);
        }

        private bool ProcessKeyboard(Win32Native.Msg message)
        {
            uint kind = message.Message;
            if (kind != 0x100 && kind != 0x101 && kind != 0x104 && kind != 0x105)
                return false;
            bool up = kind == 0x101 || kind == 0x105;
            int key = message.WParam.ToInt32();
            FocusTicket? owner = _focus.Current;
            if (!owner.HasValue || !CanSend(owner.Value))
                return true;
            bool eaten = _ime.ProcessKey(kind, new UIntPtr(unchecked((uint)key)), message.LParam);
            if (!_focus.Allows(owner.Value))
                return true;
            if (!_browserKeys.TryGetValue(key, out bool browser))
            {
                if (up)
                    return true;
                bool shortcut =
                    (Win32Native.GetKeyState(0x11) & 0x8000) != 0 || (Win32Native.GetKeyState(0x12) & 0x8000) != 0;
                bool layoutText = shortcut && LayoutProducesText(message);
                browser = NativeKeyRoute.UsesBrowser(key, eaten, shortcut, layoutText);
                _browserKeys[key] = browser;
            }
            if (up)
                _browserKeys.Remove(key);
            if (browser)
            {
                if (NativeKeyName.TryRead(key, out string name))
                    QueuePageOperation(done =>
                    {
                        if (NativeKey == null)
                            done(false);
                        else
                            NativeKey(name, up, done);
                    });
                return true;
            }
            return eaten || up || !_focus.Current.HasValue;
        }

        private static bool LayoutProducesText(Win32Native.Msg message)
        {
            var state = new byte[256];
            if (!Win32Native.GetKeyboardState(state))
                return false;
            var text = new char[8];
            uint scan = (uint)((message.LParam.ToInt64() >> 16) & 0xFF);
            // Flag 4 probes translation without changing the keyboard's dead-key state.
            int count = Win32Native.ToUnicodeEx(
                unchecked((uint)message.WParam.ToInt32()),
                scan,
                state,
                text,
                text.Length,
                4,
                Win32Native.GetKeyboardLayout(0)
            );
            if (count < 0)
                return true;
            for (int index = 0; index < Math.Min(count, text.Length); index++)
                if (!char.IsControl(text[index]))
                    return true;
            return false;
        }

        private void Enqueue(KeyStroke stroke, FocusTicket owner)
        {
            if (stroke.CountsAsComposition)
            {
                _pendingCompositionKeys.TryGetValue(owner, out int count);
                _pendingCompositionKeys[owner] = count + 1;
            }
            _strokes.Enqueue((stroke, owner));
        }

        private bool TryDequeue(out (KeyStroke Stroke, FocusTicket Owner) stroke)
        {
            lock (_strokeLock)
            {
                return _strokes.TryDequeue(out stroke);
            }
        }

        private void Finish()
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0)
            {
                return;
            }
            _focus.Detach();

            _modeButton?.Dispose();
            _modeButton = null;

            if (_profiles != null)
            {
                _profiles.Dispose();
                _profiles = null;
            }

            if (_profileNotifications != null)
            {
                _profileNotifications.Dispose();
                _profileNotifications = null;
            }

            if (_ime != null)
            {
                _ime.Dispose();
                _ime = null;
            }

            if (_lease != null)
            {
                _lease.Restore();
            }

            if (_window != null)
            {
                _window.Close();
            }

            Win32Native.PostQuitMessage(0);
        }
    }
}
