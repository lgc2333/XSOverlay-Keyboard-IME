using System;
using System.Collections.Concurrent;
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
    /// See ADR 0006, ADR 0007, ADR 0008 and ADR 0016.
    /// </summary>
    internal sealed class CompositionService : IUIElementObserver
    {
        private readonly HostWindow _window;
        private readonly ForegroundLease _lease;
        private readonly KeyFeeder _feeder;
        private readonly Action<string> _onCommitted;
        private readonly InlinePreeditWatch _preeditWatch = new InlinePreeditWatch();
        private readonly ConcurrentQueue<UIElementEvent> _elements = new ConcurrentQueue<UIElementEvent>();

        private ThreadManagerBridge _bridge;
        private volatile bool _open;
        private volatile bool _composing;
        private uint _elementId;

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

            if (HasComposition())
            {
                _composing = true;
                return;
            }

            // The IME composed nothing, so the control holds plain text: pass it on now.
            _composing = false;
            string typed = ReadControlText();
            if (typed.Length > 0)
            {
                Win32Native.SetWindowTextW(_window.Editor, string.Empty);
                _onCommitted(typed);
            }
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

            string composition = ImmTextReader.ReadComposition(_window.Editor);
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

        /// <summary>
        /// The control text holds what already landed. The IMM result covers a commit that
        /// the control has not taken yet, as ADR 0006 requires.
        /// </summary>
        private string ReadControlText()
        {
            string text = ImmTextReader.ReadWindowText(_window.Editor);
            return text.Length > 0 ? text : ImmTextReader.ReadResult(_window.Editor);
        }

        private bool HasComposition()
        {
            return ImmTextReader.ReadComposition(_window.Editor).Length > 0;
        }

        private void Finish(string committed)
        {
            _open = false;
            _composing = false;
            _elementId = 0;
            Win32Native.SetWindowTextW(_window.Editor, string.Empty);
            _lease.Restore();
            _bridge?.ImeOpen?.Close();
            HostLog.Info("committed '" + committed + "'");

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

            _elementId = elementId;
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
            if (!_open || elementId != _elementId)
            {
                return;
            }

            Finish(ReadControlText());
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
