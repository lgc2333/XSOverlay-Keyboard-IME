using System;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// One key press as the keyboard page reported it, holding no game type. Keeping
    /// this plain is what lets the take-over decision be tested. See ADR 0007.
    /// </summary>
    internal readonly struct PageKey
    {
        private static readonly string[] NoNames = Array.Empty<string>();

        internal PageKey(bool isSinglePress, string[] names, string text)
        {
            IsSinglePress = isSinglePress;
            Names = names ?? NoNames;
            Text = text ?? string.Empty;
        }

        /// <summary>True for one complete press, not a repeat or a release.</summary>
        internal bool IsSinglePress { get; }

        /// <summary>The names of the pressed keys, for keys that carry no text.</summary>
        internal string[] Names { get; }

        /// <summary>The single character the page resolved, including any shift.</summary>
        internal string Text { get; }
    }
}
