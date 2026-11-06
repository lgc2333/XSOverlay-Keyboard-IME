using HarmonyLib;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>
    /// Drains main-thread work once per game frame. The plugin cannot use its own Update:
    /// the game destroys the loader's manager object, so no Unity callback reaches the plugin.
    /// </summary>
    [HarmonyPatch(typeof(XSOverlay.InputManager), "Update")]
    internal static class UpdatePumpPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            MainThreadDispatcher.Drain();
        }
    }
}
