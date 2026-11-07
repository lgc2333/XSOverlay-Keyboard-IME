using System;
using System.Collections.Generic;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// What the page needs to draw the composer's own state: whether it holds the focus,
    /// the IME this thread composes with, and the IMEs it may switch to. See ADR 0035
    /// and ADR 0039.
    /// </summary>
    internal sealed class ComposerState
    {
        private static readonly ImeChoice[] NoProfiles = Array.Empty<ImeChoice>();

        /// <summary>Nothing is shown, because the composer has no focus.</summary>
        internal static readonly ComposerState Hidden = new ComposerState(false, string.Empty);

        internal ComposerState(bool active, string ime)
            : this(active, ime, NoProfiles, -1) { }

        internal ComposerState(bool active, string ime, IReadOnlyList<ImeChoice> profiles, int activeProfile)
        {
            Active = active;
            Ime = ime;
            Profiles = profiles ?? NoProfiles;
            ActiveProfile = activeProfile;
        }

        /// <summary>True while the keyboard's composer holds the focus.</summary>
        internal bool Active { get; }

        /// <summary>The IME this thread composes with, or empty when unreadable.</summary>
        internal string Ime { get; }

        /// <summary>The IMEs this machine offers, in the order the page may ask for them.</summary>
        internal IReadOnlyList<ImeChoice> Profiles { get; }

        /// <summary>The position of the active IME in Profiles, or -1 when unknown.</summary>
        internal int ActiveProfile { get; }
    }
}
