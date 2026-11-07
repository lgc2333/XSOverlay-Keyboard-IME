using XSOverlayKeyboardIme.Ime;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page reads as the composer's state.</summary>
    internal static class ComposerStatePayload
    {
        internal static string Build(ComposerState state)
        {
            return "{\"active\":"
                + (state.Active ? "true" : "false")
                + ",\"ime\":"
                + JavaScriptString.Literal(state.Ime)
                + "}";
        }
    }
}
