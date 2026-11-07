using System.Text;
using XSOverlayKeyboardIme.Ime;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page reads as the composer's state.</summary>
    internal static class ComposerStatePayload
    {
        internal static string Build(ComposerState state)
        {
            var payload = new StringBuilder();
            payload.Append("{\"active\":").Append(state.Active ? "true" : "false");
            payload.Append(",\"ime\":").Append(JavaScriptString.Literal(state.Ime));
            payload.Append(",\"activeProfile\":").Append(state.ActiveProfile);
            payload.Append(",\"profiles\":[");
            for (int index = 0; index < state.Profiles.Count; index++)
            {
                if (index > 0)
                {
                    payload.Append(',');
                }

                payload
                    .Append("{\"name\":")
                    .Append(JavaScriptString.Literal(state.Profiles[index].Name))
                    .Append(",\"language\":")
                    .Append(JavaScriptString.Literal(state.Profiles[index].Language))
                    .Append('}');
            }

            payload.Append("]}");
            return payload.ToString();
        }
    }
}
