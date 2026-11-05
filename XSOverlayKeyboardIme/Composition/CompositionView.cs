namespace XSOverlayKeyboardIme.Composition
{
    /// <summary>What the keyboard page shows while the IME composes: the string in
    /// progress, its candidate page and the IME name for the bar's chip.</summary>
    internal sealed class CompositionView
    {
        private static readonly string[] NoCandidates = new string[0];

        private static readonly CompositionView Empty = new CompositionView(
            string.Empty,
            NoCandidates,
            -1,
            string.Empty
        );

        internal CompositionView(string preedit, string[] candidates, int selected, string ime)
        {
            Preedit = preedit;
            Candidates = candidates;
            Selected = selected;
            Ime = ime;
        }

        /// <summary>The codes the IME has not committed yet.</summary>
        internal string Preedit { get; }

        internal string[] Candidates { get; }

        /// <summary>The highlighted candidate, or -1 when the IME reports none.</summary>
        internal int Selected { get; }

        internal string Ime { get; }

        /// <summary>An idle view that still names the IME, so the bar keeps its chip.</summary>
        internal static CompositionView Idle(string ime)
        {
            return ime.Length == 0 ? Empty : new CompositionView(string.Empty, NoCandidates, -1, ime);
        }
    }
}
