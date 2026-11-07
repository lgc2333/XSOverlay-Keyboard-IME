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

        internal PageKey(PageKeyStyle style, string[] names, string text, PageModifiers modifiers)
        {
            Style = style;
            Names = names ?? NoNames;
            Text = text ?? string.Empty;
            Modifiers = modifiers;
        }

        /// <summary>How the page delivered the press. See ADR 0036.</summary>
        internal PageKeyStyle Style { get; }

        /// <summary>The names of the pressed keys, for keys that carry no text.</summary>
        internal string[] Names { get; }

        /// <summary>The single character the page resolved, including any shift.</summary>
        internal string Text { get; }

        /// <summary>The modifiers held when the press happened.</summary>
        internal PageModifiers Modifiers { get; }
    }
}
