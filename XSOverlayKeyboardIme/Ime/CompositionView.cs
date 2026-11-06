namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>What the keyboard page shows while the IME composes: the string in
    /// progress, the caret the IME holds in it, its candidate page and the IME name for
    /// the bar's chip.</summary>
    internal sealed class CompositionView
    {
        private static readonly string[] NoCandidates = System.Array.Empty<string>();

        private static readonly CompositionView Empty = new CompositionView(
            string.Empty,
            CompositionSelection.None,
            NoCandidates,
            -1,
            string.Empty,
            false
        );

        internal CompositionView(
            string preedit,
            CompositionSelection selection,
            string[] candidates,
            int selected,
            string ime,
            bool composing
        )
        {
            Preedit = preedit;
            Selection = selection;
            Candidates = candidates;
            Selected = selected;
            Ime = ime;
            Composing = composing;
        }

        /// <summary>The codes the IME has not committed yet.</summary>
        internal string Preedit { get; }

        /// <summary>The caret the IME holds inside <see cref="Preedit"/>.</summary>
        internal CompositionSelection Selection { get; }

        internal string[] Candidates { get; }

        /// <summary>The highlighted candidate of this page, or -1 when the IME reports none.</summary>
        internal int Selected { get; }

        internal string Ime { get; }

        /// <summary>True while the IME holds a composition. See ADR 0027.</summary>
        internal bool Composing { get; }

        /// <summary>The same view with a new composing string and its caret.</summary>
        internal CompositionView WithPreedit(string preedit, CompositionSelection selection)
        {
            return new CompositionView(preedit, selection, Candidates, Selected, Ime, Composing);
        }

        /// <summary>The same view with a new candidate page.</summary>
        internal CompositionView WithCandidates(string[] candidates, int selected)
        {
            return new CompositionView(Preedit, Selection, candidates, selected, Ime, Composing);
        }

        /// <summary>The same view with another IME name.</summary>
        internal CompositionView WithIme(string ime)
        {
            return new CompositionView(Preedit, Selection, Candidates, Selected, ime, Composing);
        }

        /// <summary>An idle view that still names the IME, so the bar keeps its chip.</summary>
        internal static CompositionView Idle(string ime)
        {
            return ime.Length == 0
                ? Empty
                : new CompositionView(string.Empty, CompositionSelection.None, NoCandidates, -1, ime, false);
        }
    }
}
