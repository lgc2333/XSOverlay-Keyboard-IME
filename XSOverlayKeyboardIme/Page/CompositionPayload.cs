using System.Globalization;
using System.Text;
using XSOverlayKeyboardIme.Composition;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page reads as its composition view.</summary>
    internal static class CompositionPayload
    {
        internal static string Build(CompositionView view)
        {
            var payload = new StringBuilder();
            payload.Append("{\"preedit\":").Append(JavaScriptString.Literal(view.Preedit));
            payload.Append(",\"selected\":").Append(view.Selected.ToString(CultureInfo.InvariantCulture));
            payload.Append(",\"ime\":").Append(JavaScriptString.Literal(view.Ime));
            payload.Append(",\"candidates\":[");
            for (int index = 0; index < view.Candidates.Length; index++)
            {
                if (index > 0)
                {
                    payload.Append(',');
                }

                payload.Append(JavaScriptString.Literal(view.Candidates[index] ?? string.Empty));
            }

            payload.Append("]}");
            return payload.ToString();
        }
    }
}
