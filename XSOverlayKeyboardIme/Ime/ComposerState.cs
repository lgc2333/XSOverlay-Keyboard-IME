namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// What the page needs to draw the composer's own state: whether it holds the focus,
    /// and the IME this thread composes with while it does. See ADR 0035.
    /// </summary>
    internal sealed class ComposerState
    {
        /// <summary>Nothing is shown, because the composer has no focus.</summary>
        internal static readonly ComposerState Hidden = new ComposerState(false, string.Empty);

        internal ComposerState(bool active, string ime)
        {
            Active = active;
            Ime = ime;
        }

        /// <summary>True while the keyboard's composer holds the focus.</summary>
        internal bool Active { get; }

        /// <summary>The IME this thread composes with, or empty when unreadable.</summary>
        internal string Ime { get; }
    }
}
