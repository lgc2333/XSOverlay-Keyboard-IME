using System.Linq;
using XSOverlayKeyboardIme.Ime;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The object literal the page reads as the composer's state.</summary>
    internal static class ComposerStatePayload
    {
        internal static string Build(ComposerState state)
        {
            return JavaScriptJson.Serialize(
                new
                {
                    active = state.Active,
                    ime = state.Ime,
                    activeProfile = state.ActiveProfile,
                    profiles = state.Profiles.Select(profile => new
                    {
                        name = profile.Name,
                        language = profile.Language,
                    }),
                }
            );
        }
    }
}
