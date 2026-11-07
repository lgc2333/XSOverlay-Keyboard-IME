using System.Globalization;
using System.Text;
using XSOverlayKeyboardIme.Ime;

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
            payload.Append(",\"selectionStart\":").Append(view.Selection.Start.ToString(CultureInfo.InvariantCulture));
            payload.Append(",\"selectionEnd\":").Append(view.Selection.End.ToString(CultureInfo.InvariantCulture));
            payload.Append(",\"composing\":").Append(view.Composing ? "true" : "false");
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
