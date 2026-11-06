using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>
    /// Answers a repeated keyboard focus request for the overlay that already holds it,
    /// so the game does not release and regrant the focus. See ADR 0021.
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
