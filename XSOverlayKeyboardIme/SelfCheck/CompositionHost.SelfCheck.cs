using BepInEx.Configuration;
using XSOverlayKeyboardIme.SelfCheck;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// The self-check half of the host. This file is only compiled in a self-check build,
    /// so the shipping plugin has no self-check code at all.
    /// </summary>
    internal sealed partial class CompositionHost
    {
        private string _selfCheckText;
        private SelfCheckRun _selfCheck;

        internal static void StartSelfCheck(string typingText)
        {
            Launch(new CompositionHost { _selfCheckText = typingText });
        }

        /// <summary>Runs the scripted composition instead of the live take-over.</summary>
        private bool TryStartSelfCheck(KeyFeeder feeder)
        {
            if (_selfCheckText == null)
            {
                return false;
            }

            _selfCheck = new SelfCheckRun(_window, feeder, _selfCheckText, Finish);
            _bridge = ThreadManagerBridge.Open(_selfCheck);
            if (_bridge == null)
            {
                return true;
            }

            _window.PumpPending();
            _lease.BeginAttach();
            _lease.Acquire();
            _selfCheck.Start(_bridge);
            return true;
        }

        private bool HandleSelfCheckMessage(uint message)
        {
            if (_selfCheck == null)
            {
                return false;
            }

            _selfCheck.HandleMessage(message);
            return true;
        }
    }
}

namespace XSOverlayKeyboardIme.SelfCheck
{
    /// <summary>Binds the self-check settings and starts it when enabled.</summary>
    internal static class SelfCheckMode
    {
        internal static bool StartIfEnabled(ConfigFile config)
        {
            ConfigEntry<bool> enabled = config.Bind(
                "SelfCheck",
                "Enabled",
                false,
                "启动时执行一次组字自检。仅自检构建包含该选项；打开时不接管按键。"
            );
            ConfigEntry<string> text = config.Bind(
                "SelfCheck",
                "Text",
                "nihc",
                "自检时逐键输入的文本，需为当前输入法能组成候选的编码。"
            );

            if (!enabled.Value)
            {
                return false;
            }

            Host.CompositionHost.StartSelfCheck(text.Value);
            return true;
        }
    }
}
