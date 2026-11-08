using System;
using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Page;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>
    /// Follows the keyboard overlay's visibility: puts the injected script on the page as
    /// soon as it opens, so the bar is there before the first key and the keyboard keeps
    /// its height, and cancels the composition the IME still holds when it goes away. The
    /// game calls the patched method on Unity's main thread, where the web view may be
    /// touched. See ADR 0020 and ADR 0027.
    /// </summary>
    [HarmonyPatch(typeof(XSOverlay.Overlay_Manager), "SetKeyboardVisibility")]
    internal static class KeyboardVisibilityPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool __0)
        {
            try
            {
                ChatboxIntegration.SetVisible(__0);
                if (!__0)
                {
                    ImeHost.CancelCurrent();
                    ImeHost.DeactivateComposer();
                    return;
                }

                KeyboardPage.Attach();
            }
            catch (Exception ex)
            {
                HostLog.Error("handling the keyboard visibility failed " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
