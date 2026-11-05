using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using XSOverlayKeyboardIme.Candidates;
using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Sinks;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Live composition: every taken key goes to the host editor and the IME decides
    /// when to commit. Text the IME does not compose is written back as it is typed.
    /// See ADR 0006, ADR 0007, ADR 0008, ADR 0010 and ADR 0016.
    /// </summary>
    internal sealed class CompositionService : IUIElementObserver
    {
        private readonly HostWindow _window;
        private readonly ForegroundLease _lease;
        private readonly KeyFeeder _feeder;
        private readonly Action<string> _onCommitted;
        private readonly Action<CompositionView> _onView;
        private readonly ImeIdentity _ime;
        private readonly InlinePreeditWatch _preeditWatch = new InlinePreeditWatch();
        private readonly ConcurrentQueue<UIElementEvent> _elements = new ConcurrentQueue<UIElementEvent>();
        private readonly List<KeyStroke> _strokes = new List<KeyStroke>(4);

        private ThreadManagerBridge _bridge;
        private volatile bool _open;
        private volatile bool _composing;
        private bool _committing;
        private uint _elementId;

        internal CompositionService(
            HostWindow window,
            ForegroundLease lease,
            KeyFeeder feeder,
            Action<string> onCommitted,
            Action<CompositionView> onView,
            ImeIdentity ime
        )
        {
            _window = window;
            _lease = lease;
            _feeder = feeder;
            _onCommitted = onCommitted;
            _onView = onView;
            _ime = ime;
        }

        /// <summary>Read on Unity's main thread: a commit means these keys belong to the IME.</summary>
        internal bool Composing
        {
            get { return _composing; }
        }

        internal void Attach(ThreadManagerBridge bridge)
        {
            _bridge = bridge;
        }

        internal void HandleMessage(uint message)
        {
            if (message == HostWindow.ElementMessage)
            {
                DrainElements();
            }
        }

        /// <summary>Feeds one taken key, then commits or writes through what it produced.</summary>
        internal void Send(KeyStroke stroke)
        {
            if (!_open)
            {
                Begin();
            }

            HostLog.Info("feed " + stroke.Describe());
            _feeder.Send(stroke);

            // The IME answers through this thread's queue, so dispatch before deciding:
            // the element events and the commit arrive that way.
            _window.PumpPending();

            if (stroke.EndsComposition)
            {
                Commit(stroke.Describe());
                return;
            }

            if (ShowComposition())
            {
                return;
            }

            // The IME composed nothing, so the control holds plain text: pass it on now.
            string typed = ReadControlText();
            if (typed.Length > 0)
            {
                Win32Native.SetWindowTextW(_window.Editor, string.Empty);
                _onCommitted(typed);
            }
        }

        /// <summary>Selects the candidate at a page index, through the keys the IME reads.</summary>
        internal void PickCandidate(int index)
        {
            if (!_open)
            {
                return;
            }

            if (!CandidateKeys.ForPick(index, _ime.ReadsCandidateDigits, _strokes))
            {
                HostLog.Warn("candidate " + index + " has no key on this IME");
                return;
            }

            HostLog.Info("pick candidate " + index);
            Feed(_strokes);
        }

        /// <summary>Turns the candidate page forwards or backwards.</summary>
        internal void TurnCandidates(int delta)
        {
            if (!_open || delta == 0)
            {
                return;
            }

            CandidateKeys.ForTurn(delta, _strokes);
            HostLog.Info("turn candidates " + delta);
            Feed(_strokes);
        }

        /// <summary>Feeds keys the page asked for, then reads what the IME made of them.</summary>
        private void Feed(List<KeyStroke> strokes)
        {
            foreach (KeyStroke stroke in strokes)
            {
                HostLog.Info("feed " + stroke.Describe());
                _feeder.Send(stroke);
            }

            _window.PumpPending();

            // The last key may have committed and ended the session through an element event.
            if (!_open || ShowComposition())
            {
                return;
            }

            Finish(ReadControlText());
        }

        private void Begin()
        {
            _open = true;
            _composing = false;
            _elementId = 0;
            Win32Native.SetWindowTextW(_window.Editor, string.Empty);
            _lease.Acquire();
            _bridge?.ImeOpen?.Open();
            _window.PumpPending();
        }

        /// <summary>
        /// Ends the session. A composition the IME did not commit on this key is taken as
        /// raw codes, which is what the composer shows while the IME is still composing.
        /// </summary>
        private void Commit(string reason)
        {
            if (!_open)
            {
                return;
            }

            // An element end that a cancel provokes must not close the session with the
            // control text: this path owns the committed text until it finishes.
            _committing = true;
            try
            {
                string composition = ReadPreedit();
                if (composition.Length > 0)
                {
                    HostLog.Info("the IME kept composing after " + reason + "; taking the raw codes");
                    _feeder.Send(VirtualKey.Escape, false);
                    _window.PumpPending();
                }

                string committed = ReadControlText();
                if (composition.Length > 0)
                {
                    committed += composition;
                }

                Finish(committed);
            }
            finally
            {
                _committing = false;
            }
        }

        /// <summary>
        /// The control text holds what already landed. The IMM result covers a commit that
        /// the control has not taken yet, as ADR 0006 requires.
        /// </summary>
        private string ReadControlText()
        {
            string text = ImmTextReader.ReadWindowText(_window.Editor);
            return text.Length > 0 ? text : ImmTextReader.ReadResult(_window.Editor);
        }

        private string ReadPreedit()
        {
            return ImmTextReader.ReadComposition(_window.Editor);
        }

        /// <summary>Shows the composing string when the IME holds one; false when it holds none.</summary>
        private bool ShowComposition()
        {
            string preedit = ReadPreedit();
            if (preedit.Length == 0)
            {
                _composing = false;
                return false;
            }

            _composing = true;
            Publish(preedit);
            return true;
        }

        private void Finish(string committed)
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            _composing = false;
            _elementId = 0;
            Win32Native.SetWindowTextW(_window.Editor, string.Empty);
            _lease.Restore();
            _bridge?.ImeOpen?.Close();
            HostLog.Info("committed '" + committed + "'");
            _onView(CompositionView.Idle(_ime.Name));

            if (committed.Length > 0)
            {
                _onCommitted(committed);
            }
        }

        private void DrainElements()
        {
            while (_elements.TryDequeue(out UIElementEvent element))
            {
                if (element.Kind == UIElementEventKind.Begun)
                {
                    OnElementBegun(element.ElementId);
                }
                else if (element.Kind == UIElementEventKind.Ended)
                {
                    OnElementEnded(element.ElementId);
                }
                else
                {
                    OnElementUpdated(element.ElementId);
                }
            }
        }

        private void OnElementBegun(uint elementId)
        {
            if (!_open)
            {
                return;
            }

            _composing = true;
            HostLog.Info("composition started, element " + elementId);
            ReadElement(elementId);
        }

        private void OnElementUpdated(uint elementId)
        {
            if (_open)
            {
                ReadElement(elementId);
            }
        }

        /// <summary>The IME ended its own element, which is how a candidate pick commits.</summary>
        private void OnElementEnded(uint elementId)
        {
            if (!_open || _committing || elementId != _elementId)
            {
                return;
            }

            Finish(ReadControlText());
        }

        private void ReadElement(uint elementId)
        {
            _elementId = elementId;
            string preedit = ReadPreedit();
            HostLog.Info("composing text '" + preedit + "'");

            if (_preeditWatch.Observe(preedit.Length > 0))
            {
                HostLog.Warn("the IME reported no composing string; enable inline preedit to see the typed codes");
            }

            Publish(preedit);
        }

        /// <summary>Tells the page what sits in the IME right now.</summary>
        private void Publish(string preedit)
        {
            CandidateSnapshot candidates = CandidateSnapshot.Empty;
            if (_elementId != 0 && _bridge != null)
            {
                candidates = CandidateReader.Read(_bridge.ElementManager, _elementId);
            }

            _onView(new CompositionView(preedit, candidates.Items, candidates.Selected, _ime.Name));
        }

        void IUIElementObserver.ElementBegun(uint elementId)
        {
            Enqueue(UIElementEventKind.Begun, elementId);
        }

        void IUIElementObserver.ElementUpdated(uint elementId)
        {
            Enqueue(UIElementEventKind.Updated, elementId);
        }

        void IUIElementObserver.ElementEnded(uint elementId)
        {
            Enqueue(UIElementEventKind.Ended, elementId);
        }

        /// <summary>Called on the IME thread: queue the event, never block there.</summary>
        private void Enqueue(UIElementEventKind kind, uint elementId)
        {
            _elements.Enqueue(new UIElementEvent(kind, elementId));
            _window.Post(HostWindow.ElementMessage);
        }
    }
}
