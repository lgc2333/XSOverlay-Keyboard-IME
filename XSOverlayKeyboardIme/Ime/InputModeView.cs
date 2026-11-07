namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>A present mode item, independent of whether its picture can be copied. See ADR 0042.</summary>
    internal sealed class InputModeView
    {
        internal InputModeView(
            LanguageBarIcon icon = null,
            string glyph = "",
            string font = "",
            bool enabled = true,
            string tooltip = ""
        )
        {
            Icon = icon;
            Glyph = glyph ?? string.Empty;
            Font = font ?? string.Empty;
            Enabled = enabled;
            Tooltip = tooltip ?? string.Empty;
        }

        internal LanguageBarIcon Icon { get; }
        internal string Glyph { get; }
        internal string Font { get; }
        internal bool Enabled { get; }
        internal string Tooltip { get; }
        internal string Kind =>
            Icon != null ? "image"
            : Glyph.Length > 0 ? "glyph"
            : "placeholder";
        internal string Key => Kind + ":" + Icon?.Key + ":" + Glyph + ":" + Font + ":" + Enabled + ":" + Tooltip;

        internal InputModeView Disabled() => new InputModeView(Icon, Glyph, Font, false, Tooltip);
    }
}
