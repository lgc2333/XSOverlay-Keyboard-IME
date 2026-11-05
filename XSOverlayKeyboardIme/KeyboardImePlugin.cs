using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Host;
#if IME_SELF_CHECK
using XSOverlayKeyboardIme.SelfCheck;
#endif

namespace XSOverlayKeyboardIme
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public sealed class KeyboardImePlugin : BaseUnityPlugin
    {
        private static Harmony _harmony;
        private ConfigEntry<bool> _takeoverEnabled;

        private void Awake()
        {
            HostLog.Attach(Logger);

            _takeoverEnabled = Config.Bind(
                "Takeover",
                "Enabled",
                true,
                "接管键盘输入框的按键：组字与写回。关闭时插件不做任何事。"
            );

            HostLog.Info(MyPluginInfo.PLUGIN_NAME + " " + MyPluginInfo.PLUGIN_VERSION + " loaded");

#if IME_SELF_CHECK
            if (SelfCheckMode.StartIfEnabled(Config))
            {
                return;
            }
#endif

            if (!_takeoverEnabled.Value)
            {
                HostLog.Info("take-over is off; the plugin will do nothing");
                return;
            }

            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            bool keyTaken = HasPatch(
                typeof(XSOverlay.InputManager).GetMethod(
                    "HandleKeypress",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ),
                postfix: false
            );
            bool pumpRuns = HasPatch(
                typeof(XSOverlay.InputManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic),
                postfix: true
            );
            if (!keyTaken || !pumpRuns)
            {
                HostLog.Error(
                    "patches did not apply (keys=" + keyTaken + ", pump=" + pumpRuns + "); the plugin stays idle"
                );
                return;
            }

            HostLog.Info("patched " + typeof(XSOverlay.InputManager).FullName + "; keys and frame pump are ours");
            CompositionHost.Start();
        }

        /// <summary>The game may carry other patchers, so the check asks for our own owner.</summary>
        private static bool HasPatch(MethodInfo method, bool postfix)
        {
            Patches info = method == null ? null : Harmony.GetPatchInfo(method);
            if (info == null)
            {
                return false;
            }

            IList<Patch> patches = postfix ? info.Postfixes : info.Prefixes;
            for (int index = 0; index < patches.Count; index++)
            {
                if (patches[index].owner == MyPluginInfo.PLUGIN_GUID)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            // The game destroys the loader object. The patches must outlive it, so leave them in place.
            HostLog.Info("loader object destroyed by the game; patches stay active");
        }

        private void OnApplicationQuit()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }
}
