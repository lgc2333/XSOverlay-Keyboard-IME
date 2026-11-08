using HarmonyLib;
using XSOverlayKeyboardIme.Page;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>Captures the existing layout result without another layout query. See ADR 0049.</summary>
    [HarmonyPatch(typeof(XSOverlay.KeyboardHelper), nameof(XSOverlay.KeyboardHelper.GetKeyboardLayoutInfo))]
    internal static class KeyboardLayoutPatch
    {
        [HarmonyPostfix]
        private static void Postfix(XSOverlay.KeyboardApiObject __result)
        {
            if (__result == null)
                return;
            string format = __result.keyboardFormat;
            MainThreadDispatcher.Post(() => KeyboardPage.ShowKeyboardFormat(format));
        }
    }
}
