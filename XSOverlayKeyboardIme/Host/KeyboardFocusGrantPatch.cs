using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Keeps the composer focused when the keyboard page asks for the keyboard focus a
    /// second time. The game's manager releases the current grant and grants it again,
    /// and that release blurs the input; the blur fires another request, so the two sides
    /// keep bouncing focus. A repeated request for the overlay that already holds it is
    /// answered without touching the grant.
    /// </summary>
    [HarmonyPatch(typeof(XSOverlay.KeyboardInputFocusManager), "Request")]
    internal static class KeyboardFocusGrantPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            XSOverlay.KeyboardInputFocusManager __instance,
            XSOverlay.KeyboardInputFocusManager.FocusRequest __1,
            ref bool __result
        )
        {
            if (__1 == null || !__instance.HasFocus || __instance.OwnerHandle != __1.overlayHandle)
            {
                return true;
            }

            HostLog.Info("the page asked for the keyboard focus it already holds; keeping the grant");
            __result = true;
            return false;
        }
    }
}
