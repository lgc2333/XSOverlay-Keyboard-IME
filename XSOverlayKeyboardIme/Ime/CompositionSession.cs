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
            string imeName,
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

            // A session covers exactly one composition, so it ends as soon as the IME holds
            // none: the characters went back to the page, the key ended it, or it did
            // nothing at all, as a cancel does. See ADR 0026.
            if (WriteThroughTyped() || stroke.EndsComposition || !_ime.Composing)
            {
                End();
            }
        }

        /// <summary>Takes the foreground and opens the session. See ADR 0026.</summary>
        private void Begin()
        {
            _open = true;
            _lease.Acquire();
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

        /// <summary>Ends the session and hands the foreground back.</summary>
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
