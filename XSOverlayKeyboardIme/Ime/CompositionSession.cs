using System;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// One take-over session: from the first taken key to the commit that ends it. Keys
    /// go to the IME, which reports what it did with them; text it did not compose is
    /// written back as it is typed. See ADR 0007, ADR 0008 and ADR 0026.
    /// </summary>
    internal sealed class CompositionSession
    {
        private readonly ImeContext _ime;
        private readonly HostWindow _window;
        private readonly ForegroundLease _lease;
        private readonly KeyFeeder _feeder;
        private readonly Action<string> _onCommitted;
        private readonly Action<CompositionView> _onView;
        private readonly InlinePreeditWatch _preeditWatch = new InlinePreeditWatch();

        private CompositionView _view;
        private bool _open;

        internal CompositionSession(
            ImeContext ime,
            HostWindow window,
            ForegroundLease lease,
            KeyFeeder feeder,
            Action<string> onCommitted,
            Action<CompositionView> onView
        )
        {
            _ime = ime;
            _window = window;
            _lease = lease;
            _feeder = feeder;
            _onCommitted = onCommitted;
            _onView = onView;
            _view = CompositionView.Idle();

            _ime.PreEditChanged += HandlePreEdit;
            _ime.CandidatesChanged += HandleCandidates;
            _ime.Committed += HandleCommit;
        }

        /// <summary>Read on Unity's main thread: a live composition means these keys belong to the IME.</summary>
        internal bool Composing
        {
            get { return _ime.Composing; }
        }

        /// <summary>Publishes the idle view, so the bar is on the page before the first key.</summary>
        internal void Start()
        {
            Publish();
        }

        /// <summary>Feeds one taken key, then acts on what the IME did with it.</summary>
        internal bool Send(KeyStroke stroke, FocusTicket owner, Func<bool> valid)
        {
            // Releases must reach Windows even after the keyboard has closed. They must
            // not take foreground back from the user's next window. See ADR 0040.
            if (stroke.Kind != KeyStrokeKind.ModifierUp)
            {
                if (!valid() || !_lease.Ensure() || !valid())
                {
                    return false;
                }

                _open = true;
            }

            HostLog.Info("feed " + stroke.Describe());
            if (!_feeder.Send(stroke, owner))
            {
                if (!_ime.Composing)
                    End();
                return false;
            }

            if (stroke.Kind == KeyStrokeKind.ModifierDown)
            {
                // Keep the same input target until the release or next ordinary key.
                _open = true;
                return true;
            }

            // A commit ends the session from inside the callback, which settles the key.
            if (!_open)
            {
                return true;
            }

            // A session covers exactly one composition, so it ends as soon as the IME holds
            // none: the text was committed, the characters went back to the page, or the
            // IME cancelled. A space can convert one segment and keep composing, so the key
            // itself never ends the session. See ADR 0026 and ADR 0033.
            if (WriteThroughTyped() || !_ime.Composing)
            {
                End();
            }
            return true;
        }

        /// <summary>Activates with foreground held, without synthesizing composition input. See ADR 0045.</summary>
        internal bool ActivateProfile(ImeProfiles profiles, LanguageProfile profile, Func<bool> valid)
        {
            if (!valid() || !_lease.Ensure() || !valid())
                return false;
            bool activated = profiles.Activate(profile);
            if (!_ime.Composing)
                End();
            return valid() && activated;
        }

        internal bool BeginModeAction()
        {
            return _lease.Ensure();
        }

        /// <summary>
        /// Hands over the characters the IME chose not to compose. While it composes it
        /// keeps the keys, so the buffer belongs to no composition and is left alone.
        /// </summary>
        private bool WriteThroughTyped()
        {
            if (_ime.Composing)
            {
                return false;
            }

            string typed = _window.TakeTyped();
            if (typed.Length == 0)
            {
                return false;
            }

            HostLog.Info("the IME passed through '" + typed + "'");
            _onCommitted(typed);
            return true;
        }

        /// <summary>Ends only composition; the composer focus lease outlives a commit. See ADR 0050.</summary>
        private void End()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            _view = CompositionView.Idle();
            Publish();
        }

        private void HandlePreEdit(string text, int selectionStart, int selectionEnd)
        {
            // The page cancels on a changed caret only while this flag is set, so it has to
            // carry what the IME holds, not a constant. See ADR 0037.
            _view = _view
                .WithPreedit(text, new CompositionSelection(selectionStart, selectionEnd))
                .WithComposing(_ime.Composing);
            if (_ime.Composing && _preeditWatch.Observe(text.Length > 0))
            {
                HostLog.Warn("the IME reported no composing string; enable inline preedit to see the typed codes");
            }

            Publish();
        }

        private void HandleCandidates(string[] candidates, int selected)
        {
            _view = _view.WithCandidates(candidates, selected).WithComposing(_ime.Composing);
            Publish();
        }

        /// <summary>The IME finished a composition; this is the text the page receives. See ADR 0032.</summary>
        private void HandleCommit(string text)
        {
            HostLog.Info("committed '" + text + "'");
            End();
            if (text.Length > 0)
            {
                _onCommitted(text);
            }
        }

        /// <summary>Tells the page what sits in the IME right now.</summary>
        private void Publish()
        {
            _onView(_view);
        }
    }
}
