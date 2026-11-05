using BepInEx;
using BepInEx.Logging;

namespace XSOverlayKeyboardIme
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public sealed class KeyboardImePlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo(MyPluginInfo.PLUGIN_NAME + " " + MyPluginInfo.PLUGIN_VERSION + " loaded");
        }
    }
}
