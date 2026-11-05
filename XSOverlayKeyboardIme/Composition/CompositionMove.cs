namespace XSOverlayKeyboardIme.Composition
{
    /// <summary>What the host must do next in response to an IME event.</summary>
    internal enum CompositionMove
    {
        None,
        WaitForComposition,
        ReportNotComposing,
        ReadCompositionAndCandidates,
        ReadResult,
    }
}
