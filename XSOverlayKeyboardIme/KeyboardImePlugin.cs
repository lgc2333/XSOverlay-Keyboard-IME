using BepInEx;
using BepInEx.Configuration;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Host;

namespace XSOverlayKeyboardIme
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public sealed class KeyboardImePlugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> _selfCheckEnabled;
        private ConfigEntry<string> _selfCheckText;

        private void Awake()
        {
            HostLog.Attach(Logger);

            _selfCheckEnabled = Config.Bind(
                "SelfCheck",
                "Enabled",
                false,
                "启动时执行一次组字自检：抢占前台、向隐藏输入框喂键、读回候选与提交结果。"
            );
            _selfCheckText = Config.Bind(
                "SelfCheck",
                "Text",
                "nihc",
                "自检时逐键输入的文本，需为当前输入法能组成候选的编码。"
            );

            HostLog.Info(MyPluginInfo.PLUGIN_NAME + " " + MyPluginInfo.PLUGIN_VERSION + " loaded");

            if (_selfCheckEnabled.Value)
            {
                CompositionHost.Start(_selfCheckText.Value);
            }
        }
    }
}
