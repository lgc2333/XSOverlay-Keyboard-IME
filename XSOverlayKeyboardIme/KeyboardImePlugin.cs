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
        private ConfigEntry<bool> _takeoverEnabled;
        private Harmony _harmony;

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

            MethodInfo target = typeof(XSOverlay.InputManager).GetMethod(
                "HandleKeypress",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Patches applied = target == null ? null : Harmony.GetPatchInfo(target);
            if (applied == null || applied.Prefixes.Count == 0)
            {
                HostLog.Error("take-over patch did not apply; keys stay with the game");
                return;
            }

            HostLog.Info("take-over patched onto " + typeof(XSOverlay.InputManager).FullName);
            CompositionHost.Start();
        }

        private void Update()
        {
            MainThreadDispatcher.Drain();
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }
}
