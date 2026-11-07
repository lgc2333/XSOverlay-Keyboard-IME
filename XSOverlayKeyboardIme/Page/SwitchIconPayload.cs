using XSOverlayKeyboardIme.Ime;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page turns back into the switch's image. See ADR 0028.</summary>
    internal static class SwitchIconPayload
    {
        /// <summary>An IME with no bar item sends its null icon as the literal null.</summary>
        internal static string Build(InputModeView view)
        {
            return JavaScriptJson.Serialize(
                view == null
                    ? null
                    : new
                    {
                        kind = view.Kind,
                        enabled = view.Enabled,
                        tooltip = view.Tooltip,
                        glyph = view.Glyph,
                        font = view.Font,
                        width = view.Icon?.Width ?? 0,
                        height = view.Icon?.Height ?? 0,
                        rgba = view.Icon?.Rgba,
                        tint = view.Icon?.Tint ?? false,
                    }
            );
        }
    }
}
