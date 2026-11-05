using System;
using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Host;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// Runs before the game handles a keyboard page key. Taking the key here also stops
    /// the enter and escape focus release, so a composing Enter reaches the IME. See ADR 0007.
    /// </summary>
    [HarmonyPatch(typeof(XSOverlay.InputManager), "HandleKeypress")]
    internal static class KeyboardTakeoverPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(XSOverlay.Websockets.API.Objects.KeyboardEvent keyEvent)
        {
            try
            {
                CompositionHost host = CompositionHost.Current;
                return host == null || !host.TryAcceptKey(keyEvent);
            }
            catch (Exception ex)
            {
                HostLog.Error("take-over failed " + ex.GetType().Name + ": " + ex.Message);
                return true;
            }
        }
    }
}
