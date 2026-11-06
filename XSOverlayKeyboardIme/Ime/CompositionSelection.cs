namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The range the IME highlights inside its composing string. It is the caret the user
    /// moves with the cursor keys, and the composer draws it. See ADR 0031.
    /// </summary>
    internal readonly struct CompositionSelection
    {
        internal CompositionSelection(int start, int end)
        {
            Start = start;
            End = end;
        }

        internal int Start { get; }

        internal int End { get; }

        /// <summary>No composition, so nothing is highlighted.</summary>
        internal static CompositionSelection None
        {
            get { return new CompositionSelection(0, 0); }
        }
    }
}
