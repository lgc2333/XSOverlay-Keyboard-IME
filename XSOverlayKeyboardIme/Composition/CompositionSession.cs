namespace XSOverlayKeyboardIme.Composition
{
    /// <summary>Maps IME events to the next host action. See ADR 0006 and ADR 0013.</summary>
    internal sealed class CompositionSession
    {
        internal CompositionPhase Phase { get; private set; } = CompositionPhase.Idle;

        internal uint ElementId { get; private set; }

        internal CompositionMove BeginTyping()
        {
            Phase = CompositionPhase.AwaitingComposition;
            ElementId = 0;
            return CompositionMove.WaitForComposition;
        }

        /// <summary>Fires one beat after the keys were fed, before the IME has answered.</summary>
        internal CompositionMove Beat()
        {
            if (Phase == CompositionPhase.AwaitingComposition)
            {
                Phase = CompositionPhase.Idle;
                return CompositionMove.ReportNotComposing;
            }

            if (Phase == CompositionPhase.AwaitingResult)
            {
                Phase = CompositionPhase.Finished;
                return CompositionMove.ReadResult;
            }

            return CompositionMove.None;
        }

        internal CompositionMove ElementBegun(uint elementId)
        {
            ElementId = elementId;
            if (Phase == CompositionPhase.AwaitingComposition || Phase == CompositionPhase.Composing)
            {
                Phase = CompositionPhase.Composing;
                return CompositionMove.ReadCompositionAndCandidates;
            }

            return CompositionMove.None;
        }

        /// <summary>Reports whether the session moved to waiting for the commit result.</summary>
        internal bool Commit()
        {
            if (Phase != CompositionPhase.Composing)
            {
                return false;
            }

            Phase = CompositionPhase.AwaitingResult;
            return true;
        }

        internal CompositionMove ElementEnded(uint elementId)
        {
            if (Phase != CompositionPhase.AwaitingResult || elementId != ElementId)
            {
                return CompositionMove.None;
            }

            Phase = CompositionPhase.Finished;
            return CompositionMove.ReadResult;
        }
    }
}
