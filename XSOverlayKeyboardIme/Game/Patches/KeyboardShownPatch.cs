using System;
using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Page;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>
    /// Puts the injected script on the page as soon as the keyboard opens, so the bar is
    /// there before the first key and the keyboard keeps its height. The game calls the
    /// patched method on Unity's main thread, where the web view may be touched.
    /// See ADR 0020.
    /// </summary>
    [HarmonyPatch(typeof(XSOverlay.Overlay_Manager), "SetKeyboardVisibility")]
    internal static class KeyboardShownPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool __0)
        {
            if (!__0)
            {
                return;
            }

            try
            {
                KeyboardPage.Attach();
            }
            catch (Exception ex)
            {
                HostLog.Error("attaching the page on open failed " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
