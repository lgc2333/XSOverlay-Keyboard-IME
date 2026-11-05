using System;
using System.Collections.Concurrent;
using XSOverlayKeyboardIme.Candidates;
using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Sinks;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Live composition: every taken key goes to the host editor and the IME decides
    /// when to commit. See ADR 0006, ADR 0007 and ADR 0010.
    /// </summary>
    internal sealed class CompositionService : IUIElementObserver
    {
        private readonly HostWindow _window;
        private readonly ForegroundLease _lease;
        private readonly KeyFeeder _feeder;
        private readonly Action<string> _onCommitted;
        private readonly CompositionSession _session = new CompositionSession();
        private readonly InlinePreeditWatch _preeditWatch = new InlinePreeditWatch();
        private readonly ConcurrentQueue<UIElementEvent> _elements = new ConcurrentQueue<UIElementEvent>();

        private ThreadManagerBridge _bridge;
        private volatile bool _composing;
        private volatile bool _confirmed;
        private string _baseline = string.Empty;

        internal CompositionService(
            HostWindow window,
            ForegroundLease lease,
            KeyFeeder feeder,
            Action<string> onCommitted
        )
        {
            _window = window;
            _lease = lease;
            _feeder = feeder;
            _onCommitted = onCommitted;
        }

        /// <summary>Read by the main thread: the IME published an element, so control keys belong here.</summary>
        internal bool ConfirmedComposing
        {
            get { return _confirmed; }
        }

        internal void Attach(ThreadManagerBridge bridge)
        {
            _bridge = bridge;
        }

        internal void HandleMessage(uint message)
        {
            if (message == HostWindow.BeatMessage)
            {
                OnBeat();
            }
            else if (message == HostWindow.ElementMessage)
            {
                DrainElements();
            }
        }

        /// <summary>Feeds one taken key, starting a composition when the previous one ended.</summary>
        internal void Send(KeyStroke stroke)
        {
            if (!_composing)
            {
                Begin();
            }

            HostLog.Info("feed " + stroke.Describe());
            _feeder.Send(stroke);
        }

        private void Begin()
        {
            _composing = true;
            _session.Reset();
            _session.BeginTyping();
            _baseline = ImmTextReader.ReadWindowText(_window.Editor).Trim();
            _lease.Acquire();
            _bridge?.ImeOpen?.Open();
            _window.Post(HostWindow.BeatMessage);
        }

        private void OnBeat()
        {
            CompositionMove move = _session.Beat();
            if (move == CompositionMove.ReportNotComposing)
            {
                HostLog.Warn("the IME did not start composing; the key was dropped");
                End();
            }
            else if (move == CompositionMove.ReadResult)
            {
                FinishComposition();
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
            if (_session.ElementBegun(elementId) != CompositionMove.ReadCompositionAndCandidates)
            {
                return;
            }

            _confirmed = true;
            HostLog.Info("composition started, element " + elementId);
            ReadElement(elementId);
        }

        private void OnElementUpdated(uint elementId)
        {
            if (_session.Phase != CompositionPhase.Composing)
            {
                return;
            }

            ReadElement(elementId);
        }

        private void OnElementEnded(uint elementId)
        {
            if (_session.ElementEnded(elementId) != CompositionMove.ReadResult)
            {
                return;
            }

            FinishComposition();
        }

        private void ReadElement(uint elementId)
        {
            string composition = ImmTextReader.ReadComposition(_window.Editor);
            HostLog.Info("composing text '" + composition + "'");

            if (_preeditWatch.Observe(composition.Length > 0))
            {
                HostLog.Warn("the IME reported no composing string; enable inline preedit to see the typed codes");
            }

            CandidateReader.Read(_bridge.ElementManager, elementId);
        }

        /// <summary>Keeps the control text as a fallback when IMM has no result, then clears it.</summary>
        private void FinishComposition()
        {
            string committed = ImmTextReader.ReadResult(_window.Editor);
            if (committed.Length == 0)
            {
                string windowText = ImmTextReader.ReadWindowText(_window.Editor).Trim();
                committed = windowText.StartsWith(_baseline, StringComparison.Ordinal)
                    ? windowText.Substring(_baseline.Length).Trim()
                    : windowText;
            }

            Win32Native.SetWindowTextW(_window.Editor, string.Empty);
            HostLog.Info("committed text '" + committed + "'");

            if (committed.Length > 0)
            {
                _onCommitted(committed);
            }

            End();
        }

        private void End()
        {
            _composing = false;
            _confirmed = false;
            _session.Reset();
            _lease.Restore();
            _bridge?.ImeOpen?.Close();
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
