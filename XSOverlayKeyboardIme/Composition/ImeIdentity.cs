using System;

namespace XSOverlayKeyboardIme.Composition
{
    /// <summary>
    /// The IME this thread composes with. Only its display name is known, so the name
    /// decides whether candidates take page digits or arrow keys. See ADR 0009 and ADR 0010.
    /// </summary>
    internal readonly struct ImeIdentity
    {
        private static readonly string[] RimeNames = { "rime", "weasel", "小狼毫" };

        internal static readonly ImeIdentity Unknown = new ImeIdentity(string.Empty);

        internal ImeIdentity(string name)
        {
            Name = name ?? string.Empty;
            ReadsCandidateDigits = Matches(Name, RimeNames);
        }

        internal string Name { get; }

        /// <summary>Rime binds candidate digits to selection; other IMEs only take arrows.</summary>
        internal bool ReadsCandidateDigits { get; }

        private static bool Matches(string name, string[] needles)
        {
            if (name.Length == 0)
            {
                return false;
            }

            foreach (string needle in needles)
            {
                if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
