using System;
using System.Collections.Generic;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// One keyboard profile TSF lists for a language, and whether this thread has it active.
    /// Kept free of TSF types so the pick can be tested. See ADR 0035.
    /// </summary>
    internal readonly struct LanguageProfile
    {
        internal LanguageProfile(Guid service, ushort language, Guid profile, bool active)
        {
            Service = service;
            Language = language;
            Profile = profile;
            Active = active;
        }

        /// <summary>The text service that owns the profile.</summary>
        internal Guid Service { get; }

        internal ushort Language { get; }

        internal Guid Profile { get; }

        /// <summary>True when this thread composes with this profile.</summary>
        internal bool Active { get; }

        /// <summary>The profile this thread has active, or false when the page holds none.</summary>
        internal static bool TryFindActive(IReadOnlyList<LanguageProfile> profiles, out LanguageProfile active)
        {
            for (int index = 0; index < profiles.Count; index++)
            {
                if (profiles[index].Active)
                {
                    active = profiles[index];
                    return true;
                }
            }

            active = default;
            return false;
        }
    }
}
