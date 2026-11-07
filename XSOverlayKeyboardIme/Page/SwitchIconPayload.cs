using XSOverlayKeyboardIme.Ime;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page turns back into the switch's image. See ADR 0028.</summary>
    internal static class SwitchIconPayload
    {
        /// <summary>An IME with no bar item sends its null icon as the literal null.</summary>
        internal static string Build(LanguageBarIcon icon)
        {
            return JavaScriptJson.Serialize(
                icon == null
                    ? null
                    : new
                    {
                        width = icon.Width,
                        height = icon.Height,
                        rgba = icon.Rgba,
                    }
            );
        }
    }
}
