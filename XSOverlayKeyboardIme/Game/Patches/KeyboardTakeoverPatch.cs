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
        private static bool Prefix(XSOverlay.Websockets.API.Objects.KeyboardEvent __0)
        {
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

                var key = new PageKey(
                    __0 != null && __0.keyPressStyle == XSOverlay.Websockets.API.Enums.KeyPressStyle.Standard,
                    __0?.value,
                    __0?.webValue
                );

                if (!host.TryAcceptKey(key))
                {
                    Refuse("the key was not taken: " + DescribeKey(__0));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                HostLog.Error("take-over failed " + ex.GetType().Name + ": " + ex.Message);
                return true;
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

        private static string DescribeKey(XSOverlay.Websockets.API.Objects.KeyboardEvent keyEvent)
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
                + (keyEvent.webValue ?? "(null)");
        }
    }
}
