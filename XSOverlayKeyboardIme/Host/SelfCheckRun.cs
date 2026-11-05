using System;
using System.Collections.Concurrent;
using XSOverlayKeyboardIme.Candidates;
using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Sinks;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>One startup run: prove composing, candidates and the commit result.</summary>
    internal sealed class SelfCheckRun : IUIElementObserver
    {
        private const int MaxAttempts = 3;
        private const int ClearStrokeCount = 12;

        private readonly HostWindow _window;
        private readonly KeyFeeder _feeder;
        private readonly string _typingText;
        private readonly Action _onFinished;
        private readonly CompositionSession _session = new CompositionSession();
        private readonly InlinePreeditWatch _preeditWatch = new InlinePreeditWatch();
        private readonly ConcurrentQueue<UIElementEvent> _pending = new ConcurrentQueue<UIElementEvent>();

        private ThreadManagerBridge _bridge;
        private string _baselineWindowText = string.Empty;
        private int _attempts;
        private bool _finished;

        internal SelfCheckRun(HostWindow window, KeyFeeder feeder, string typingText, Action onFinished)
        {
            _window = window;
            _feeder = feeder;
            _typingText = typingText;
            _onFinished = onFinished;
        }

        internal void Start(ThreadManagerBridge bridge)
        {
            _bridge = bridge;
            HostLog.Info("self-check starts, typing '" + _typingText + "'");
            BeginAttempt();
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
            else if (message == HostWindow.ObserveMessage)
            {
                ObserveComposition();
            }
        }

        private void BeginAttempt()
        {
            _attempts++;
            _session.BeginTyping();
            _baselineWindowText = ImmTextReader.ReadWindowText(_window.Editor).Trim();
            _feeder.Send(_typingText);
            _window.Post(HostWindow.BeatMessage);
        }

        private void OnBeat()
        {
            HostLog.Info("beat: editor text '" + ImmTextReader.ReadWindowText(_window.Editor) + "'");
            CompositionMove move = _session.Beat();
            if (move == CompositionMove.ReportNotComposing)
            {
                RetryOrGiveUp();
            }
            else if (move == CompositionMove.ReadResult)
            {
                ReportResult();
                Finish();
            }
        }

        private void RetryOrGiveUp()
        {
            if (_attempts >= MaxAttempts)
            {
                HostLog.Warn(
                    "the IME did not start composing after "
                        + _attempts
                        + " attempts; check that the active IME is in Chinese mode and has inline preedit enabled"
                );
                Finish();
                return;
            }

            HostLog.Info("no composition yet, retrying (" + _attempts + " of " + MaxAttempts + ")");
            ClearEditor();

            // Flip the IME between its English and Chinese states. The state is not
            // readable from here, so the retry covers both instead of guessing.
            _feeder.Send(VirtualKey.Shift, false);
            BeginAttempt();
        }

        private void ClearEditor()
        {
            for (int index = 0; index < ClearStrokeCount; index++)
            {
                _feeder.Send(VirtualKey.Back, false);
            }
        }

        private void DrainElements()
        {
            while (_pending.TryDequeue(out UIElementEvent element))
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
            CompositionMove move = _session.ElementBegun(elementId);
            if (move != CompositionMove.ReadCompositionAndCandidates)
            {
                return;
            }

            HostLog.Info("composition started, element " + elementId);
            ReadElement(elementId);

            // The IME publishes the composing string one beat after the element.
            _window.Post(HostWindow.ObserveMessage);
        }

        private void OnElementUpdated(uint elementId)
        {
            if (_session.Phase != CompositionPhase.Composing)
            {
                return;
            }

            ReadElement(elementId);
        }

        /// <summary>Second look at the composing string, then commit.</summary>
        private void ObserveComposition()
        {
            if (_session.Phase != CompositionPhase.Composing)
            {
                return;
            }

            ReadElement(_session.ElementId);
            if (!_session.Commit())
            {
                return;
            }

            _feeder.Send(VirtualKey.Space, false);

            // Fallback for services that commit without ending the element.
            _window.Post(HostWindow.BeatMessage);
        }

        private void OnElementEnded(uint elementId)
        {
            if (_session.ElementEnded(elementId) != CompositionMove.ReadResult)
            {
                return;
            }

            ReportResult();
            Finish();
        }

        private void ReadElement(uint elementId)
        {
            string composition = ImmTextReader.ReadComposition(_window.Editor);
            bool hasText = composition.Length > 0;
            HostLog.Info("composing text '" + composition + "'");

            if (_preeditWatch.Observe(hasText))
            {
                HostLog.Warn("the IME reported no composing string; enable inline preedit to see the typed codes");
            }

            CandidateReader.Read(_bridge.ElementManager, elementId);
        }

        /// <summary>Falls back to the control text when IMM has no result, then clears it.</summary>
        private void ReportResult()
        {
            string result = ImmTextReader.ReadResult(_window.Editor);
            string windowText = ImmTextReader.ReadWindowText(_window.Editor).Trim();
            string committed = windowText.StartsWith(_baselineWindowText, StringComparison.Ordinal)
                ? windowText.Substring(_baselineWindowText.Length).Trim()
                : windowText;

            HostLog.Info("committed text '" + result + "' from the window '" + committed + "'");
            Win32Native.SetWindowTextW(_window.Editor, string.Empty);
        }

        private void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            HostLog.Info("self-check finished");
            _onFinished();
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
            _pending.Enqueue(new UIElementEvent(kind, elementId));
            _window.Post(HostWindow.ElementMessage);
        }
    }
}
