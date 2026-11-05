namespace XSOverlayKeyboardIme.Composition
{
    /// <summary>Where one composition attempt currently stands.</summary>
    internal enum CompositionPhase
    {
        Idle,
        AwaitingComposition,
        Composing,
        AwaitingResult,
        Finished,
    }
}
