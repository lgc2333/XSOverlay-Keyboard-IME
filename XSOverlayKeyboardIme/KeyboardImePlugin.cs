using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Game;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Page;

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

            if (!_takeoverEnabled.Value)
            {
                HostLog.Info("take-over is off; the plugin will do nothing");
                return;
            }

            // A missing native library is a distribution error, so it stops here: patching
            // without a working IME context would only swallow the keys.
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            if (!ImeLibraries.TryLoad(pluginDirectory, out string error))
            {
                HostLog.Error("the native IME library did not load, so nothing was patched: " + error);
                return;
            }

            HostLog.Info("native IME library ready: " + ImeLibraries.Describe());

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
            bool pageShown = HasPatch(
                typeof(XSOverlay.Overlay_Manager).GetMethod(
                    "SetKeyboardVisibility",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ),
                postfix: true
            );
            if (!keyTaken || !pumpRuns)
            {
                HostLog.Error(
                    "patches did not apply (keys=" + keyTaken + ", pump=" + pumpRuns + "); the plugin stays idle"
                );
                return;
            }

            if (!pageShown)
            {
                HostLog.Warn("the page attach patch is missing; the candidate bar appears on the first key");
            }

            bool focusKept = HasPatch(typeof(XSOverlay.KeyboardInputFocusManager).GetMethod("Request"), postfix: false);
            if (!focusKept)
            {
                HostLog.Warn("the focus grant patch is missing; the composer focus may flicker");
            }

            HostLog.Info("patched " + typeof(XSOverlay.InputManager).FullName + "; keys and frame pump are ours");

            // The root wires the host to the page, so neither layer knows the other.
            KeyboardPage.SwitchRequested = () =>
            {
                ImeHost host = ImeHost.Current;
                if (host != null)
                {
                    host.RequestSwitch();
                }
            };

            KeyboardPage.CancelRequested = ImeHost.CancelCurrent;
            KeyboardPage.ChatboxChanged = ChatboxIntegration.Update;
            KeyboardPage.PageUnavailable = ChatboxIntegration.Hide;

            KeyboardPage.ProfileRequested = index =>
            {
                ImeHost host = ImeHost.Current;
                if (host != null)
                {
                    host.RequestProfile(index);
                }
            };

            KeyboardPage.ComposerActivationChanged = active =>
            {
                ImeHost host = ImeHost.Current;
                if (host != null)
                {
                    host.SetComposerActive(active);
                }
            };

            ImeHost.Start(
                text => MainThreadDispatcher.Post(() => KeyboardPage.WriteBack(text)),
                view => MainThreadDispatcher.Post(() => KeyboardPage.Show(view)),
                icon => MainThreadDispatcher.Post(() => KeyboardPage.ShowSwitchIcon(icon)),
                state => MainThreadDispatcher.Post(() => KeyboardPage.ShowComposerState(state))
            );
            MainThreadDispatcher.Post(KeyboardPage.Attach);
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
                ChatboxIntegration.Stop();
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }
}
