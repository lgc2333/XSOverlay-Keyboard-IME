using System;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// One take-over session: from the first taken key to the commit that ends it. Keys
    /// go to the IME, which reports what it did with them; text it did not compose is
    /// written back as it is typed. See ADR 0007, ADR 0008 and ADR 0025.
    /// </summary>
    internal sealed class CompositionSession
    {
        private const int ModeConfirmMilliseconds = 500;

        private readonly ImeContext _ime;
        private readonly HostWindow _window;
        private readonly ForegroundLease _lease;
        private readonly KeyFeeder _feeder;
        private readonly ImeCompartments _compartments;
        private readonly Action<string> _onCommitted;
        private readonly Action<CompositionView> _onView;
        private readonly InlinePreeditWatch _preeditWatch = new InlinePreeditWatch();

        private CompositionView _view;
        private bool _open;
        private bool _flipLost;

        internal CompositionSession(
            ImeContext ime,
            HostWindow window,
            ForegroundLease lease,
            KeyFeeder feeder,
            ImeCompartments compartments,
            string imeName,
            Action<string> onCommitted,
            Action<CompositionView> onView
        )
        {
            _ime = ime;
            _window = window;
            _lease = lease;
            _feeder = feeder;
            _compartments = compartments;
            _onCommitted = onCommitted;
            _onView = onView;
            _view = CompositionView.Idle(imeName);

            _ime.PreEditChanged += HandlePreEdit;
            _ime.CandidatesChanged += HandleCandidates;
            _ime.Committed += HandleCommit;
        }

        /// <summary>Read on Unity's main thread: a live composition means these keys belong to the IME.</summary>
        internal bool Composing
        {
            get { return _ime.Composing; }
        }

        /// <summary>Read on Unity's main thread: a session that has taken the keyboard.</summary>
        internal bool Open
        {
            get { return _open; }
        }

        /// <summary>Publishes the idle view, so the bar is on the page before the first key.</summary>
        internal void Start()
        {
            Publish();
        }

        /// <summary>Feeds one taken key, then acts on what the IME did with it.</summary>
        internal void Send(KeyStroke stroke)
        {
            if (!_open)
            {
                Begin();
            }

            HostLog.Info("feed " + stroke.Describe());
            _feeder.Send(stroke);

            // A commit ends the session from inside the callback, which settles the key.
            if (!_open)
            {
                return;
            }

            WriteThroughTyped();
            if (_open && stroke.EndsComposition)
            {
                End();
            }
        }

        private void Begin()
        {
            _open = true;
            _lease.Acquire();
            EnsureComposing();
        }

        /// <summary>
        /// Keys reach the IME only while it composes, so the mode is read first: a user
        /// who already types Chinese must not be flipped into English. See ADR 0025.
        /// </summary>
        private void EnsureComposing()
        {
            ImeMode mode = _ime.Mode;
            if (mode == ImeMode.Composing)
            {
                return;
            }

            if (mode == ImeMode.Unknown)
            {
                HostLog.Warn("the IME reported no input mode; the keys may arrive as plain text");
                return;
            }

            // The write is a flip, so repeating it after one that did not take would
            // toggle the user's mode on every key. One failed attempt is enough.
            if (_flipLost)
            {
                return;
            }

            if (_compartments == null || !_compartments.TryFlip())
            {
                HostLog.Warn("the IME passes keys through and the mode was not flipped");
                return;
            }

            // The report is asynchronous, so the callback is what settles the question.
            _window.PumpUntil(() => _ime.Mode != ImeMode.Passthrough, ModeConfirmMilliseconds);
            if (_ime.Mode == ImeMode.Composing)
            {
                HostLog.Info("the IME composes after the flip");
                return;
            }

            _flipLost = true;
            HostLog.Warn("the IME still passes keys through after the flip; they arrive as plain text");
        }

        /// <summary>
        /// Hands over the characters the IME let through. While it composes it keeps the
        /// keys, so the buffer belongs to no composition and is left alone.
        /// </summary>
        private void WriteThroughTyped()
        {
            if (_ime.Composing)
            {
                return;
            }

            string typed = _window.TakeTyped();
            if (typed.Length == 0)
            {
                return;
            }

            HostLog.Info("the IME passed through '" + typed + "'");
            _onCommitted(typed);
        }

        /// <summary>
        /// Ends the session and hands the foreground back. The input mode is deliberately
        /// left alone: the user keeps whatever the IME ended on. See ADR 0025.
        /// </summary>
        private void End()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            _view = CompositionView.Idle(_view.Ime);
            _lease.Restore();
            Publish();
        }

        private void HandlePreEdit(string text)
        {
            _view = new CompositionView(text, _view.Candidates, _view.Selected, _view.Ime);
            if (_ime.Composing && _preeditWatch.Observe(text.Length > 0))
            {
                HostLog.Warn("the IME reported no composing string; enable inline preedit to see the typed codes");
            }

            Publish();
        }

        private void HandleCandidates(string[] candidates, int selected)
        {
            _view = new CompositionView(_view.Preedit, candidates, selected, _view.Ime);
            Publish();
        }

        /// <summary>The IME finished a composition; this is the text the page receives.</summary>
        private void HandleCommit(string text)
        {
            if (!_open)
            {
                HostLog.Info("the IME committed '" + text + "' outside a session");
                return;
            }

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
