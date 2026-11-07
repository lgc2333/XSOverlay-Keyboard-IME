using System;
using System.Threading;
using HarmonyLib;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Game.Patches
{
    /// <summary>
    /// Runs before the game handles a keyboard page key. Taking the key here also stops
    /// the enter and escape focus release, so a composing Enter reaches the IME. See ADR 0007.
    /// </summary>
    [HarmonyPatch(typeof(XSOverlay.InputManager), "HandleKeypress")]
    internal static class KeyboardTakeoverPatch
    {
        private static long _lastRefusalTicks;

        [HarmonyPrefix]
        private static bool Prefix(
            XSOverlay.Websockets.API.Objects.KeyboardEvent __0,
            XSOverlay.InputManager __instance,
            out bool __state
        )
        {
            __state = false;
            try
            {
                ImeHost host = ImeHost.Current;
                if (host == null)
                {
                    return true;
                }

                if (!KeyboardOverlayScope.HasKeyboardFocus())
                {
                    Refuse("no focus on the keyboard overlay: " + KeyboardOverlayScope.Describe());
                    return true;
                }

                PageModifiers modifiers = KeyboardModifiers.Read(__instance);
                var key = new PageKey(
                    ReadStyle(__0),
                    __0?.value,
                    __0?.webValue,
                    modifiers,
                    KeyboardModifierInput.ReadNativeKey(__0?.value)
                );

                if (!host.TryAcceptKey(key, () => KeyboardModifierInput.ConsumeSticky(__instance)))
                {
                    Refuse("the key was not taken: " + DescribeKey(__0, modifiers));
                    return true;
                }

                __state = true;
                return false;
            }
            catch (Exception ex)
            {
                HostLog.Error("take-over failed " + ex.GetType().Name + ": " + ex.Message);
                return true;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(
            XSOverlay.Websockets.API.Objects.KeyboardEvent __0,
            XSOverlay.InputManager __instance,
            bool __state
        )
        {
            if (
                !__state
                && __0?.keyPressStyle == XSOverlay.Websockets.API.Enums.KeyPressStyle.Standard
                && ImeHost.Current?.Ready == true
                && KeyboardOverlayScope.HasKeyboardFocus()
            )
            {
                // Taken keys skip XSOverlay's handler, but must still consume its sticky keys.
                KeyboardModifierInput.ConsumeSticky(__instance);
            }
        }

        /// <summary>The page's press style, as far as the take-over cares. See ADR 0036.</summary>
        private static PageKeyStyle ReadStyle(XSOverlay.Websockets.API.Objects.KeyboardEvent keyEvent)
        {
            if (keyEvent == null)
            {
                return PageKeyStyle.Other;
            }

            switch (keyEvent.keyPressStyle)
            {
                case XSOverlay.Websockets.API.Enums.KeyPressStyle.Standard:
                    return PageKeyStyle.Standard;
                case XSOverlay.Websockets.API.Enums.KeyPressStyle.Sticky:
                    return PageKeyStyle.Sticky;
                case XSOverlay.Websockets.API.Enums.KeyPressStyle.Toggle:
                    return PageKeyStyle.Toggle;
                default:
                    return PageKeyStyle.Other;
            }
        }

        /// <summary>Explains a refusal, at most once every two seconds.</summary>
        private static void Refuse(string reason)
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - Interlocked.Read(ref _lastRefusalTicks) < TimeSpan.TicksPerSecond * 2)
            {
                return;
            }

            Interlocked.Exchange(ref _lastRefusalTicks, now);
            HostLog.Info("key refused: " + reason);
        }

        private static string DescribeKey(
            XSOverlay.Websockets.API.Objects.KeyboardEvent keyEvent,
            PageModifiers modifiers
        )
        {
            if (keyEvent == null)
            {
                return "(null)";
            }

            string values = keyEvent.value == null ? "(null)" : string.Join(",", keyEvent.value);
            return "style="
                + keyEvent.keyPressStyle
                + " values=["
                + values
                + "] web="
                + (keyEvent.webValue ?? "(null)")
                + " modifiers="
                + modifiers;
        }
    }
}
