namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>What the keyboard page shows while the IME composes: the string in
    /// progress, the caret the IME holds in it and its candidate page.</summary>
    internal sealed class CompositionView
    {
        private static readonly string[] NoCandidates = System.Array.Empty<string>();

        internal CompositionView(
            string preedit,
            CompositionSelection selection,
            string[] candidates,
            int selected,
            bool composing
        )
        {
            Preedit = preedit;
            Selection = selection;
            Candidates = candidates;
            Selected = selected;
            Composing = composing;
        }

        /// <summary>The codes the IME has not committed yet.</summary>
        internal string Preedit { get; }

        /// <summary>The caret the IME holds inside <see cref="Preedit"/>.</summary>
        internal CompositionSelection Selection { get; }

        internal string[] Candidates { get; }

        /// <summary>The highlighted candidate of this page, or -1 when the IME reports none.</summary>
        internal int Selected { get; }

        /// <summary>True while the IME holds a composition. See ADR 0027.</summary>
        internal bool Composing { get; }

        /// <summary>The same view with a new composing string and its caret.</summary>
        internal CompositionView WithPreedit(string preedit, CompositionSelection selection)
        {
            return new CompositionView(preedit, selection, Candidates, Selected, Composing);
        }

        /// <summary>The same view with a new candidate page.</summary>
        internal CompositionView WithCandidates(string[] candidates, int selected)
        {
            return new CompositionView(Preedit, Selection, candidates, selected, Composing);
        }

        /// <summary>An idle view: no codes and no candidates.</summary>
        internal static CompositionView Idle()
        {
            return new CompositionView(string.Empty, CompositionSelection.None, NoCandidates, -1, false);
        }
    }
}
