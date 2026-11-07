using System.Linq;
using XSOverlayKeyboardIme.Ime;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page reads as its composition view.</summary>
    internal static class CompositionPayload
    {
        internal static string Build(CompositionView view)
        {
            return JavaScriptJson.Serialize(
                new
                {
                    preedit = view.Preedit,
                    selected = view.Selected,
                    selectionStart = view.Selection.Start,
                    selectionEnd = view.Selection.End,
                    composing = view.Composing,
                    candidates = view.Candidates.Select(candidate => candidate ?? string.Empty),
                }
            );
        }
    }
}
