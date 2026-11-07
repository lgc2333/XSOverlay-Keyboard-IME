using System;
using System.Globalization;
using System.Text;
using XSOverlayKeyboardIme.Ime;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page turns back into the switch's image. See ADR 0028.</summary>
    internal static class SwitchIconPayload
    {
        /// <summary>An IME with no bar item sends its null icon as the literal null.</summary>
        internal static string Build(LanguageBarIcon icon)
        {
            if (icon == null)
            {
                return "null";
            }

            var payload = new StringBuilder();
            payload.Append("{\"width\":").Append(icon.Width.ToString(CultureInfo.InvariantCulture));
            payload.Append(",\"height\":").Append(icon.Height.ToString(CultureInfo.InvariantCulture));
            payload.Append(",\"rgba\":").Append(JavaScriptString.Literal(Convert.ToBase64String(icon.Rgba)));
            payload.Append('}');
            return payload.ToString();
        }
    }
}
