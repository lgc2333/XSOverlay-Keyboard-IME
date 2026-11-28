using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using WindowsInput.Native;
using XSOverlay;
using XSOverlayKeyboardIme.Diagnostics;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>Reads Return feedback from the same definition and settings as the native page.</summary>
    internal static class ComposerReturnFeedback
    {
        internal const string MessagePrefix = "xso-keyboard-ime return-feedback ";
        private static readonly Func<KeyboardDefinitions> ReadDefinition = AccessTools.MethodDelegate<
            Func<KeyboardDefinitions>
        >(AccessTools.Method(typeof(KeyboardHelper), "ParseKeyboardLayoutDefinition"));
        private static readonly Func<KeyLayout, KeyboardHelper.HKLFormat, List<List<KeyboardKey>>> GetActiveRows =
            AccessTools.MethodDelegate<Func<KeyLayout, KeyboardHelper.HKLFormat, List<List<KeyboardKey>>>>(
                AccessTools.Method(typeof(KeyboardHelper), "GetActiveRows")
            );
        private delegate bool KeycodeResolver(string code, IntPtr hkl, out VirtualKeyCode key, out uint scanCode);
        private static readonly KeycodeResolver ResolveKeycode = AccessTools.MethodDelegate<KeycodeResolver>(
            AccessTools.Method(typeof(KeyboardHelper), "TryResolveKeycode")
        );

        internal sealed class Request
        {
            public bool Numpad { get; set; }
            public string HapticTarget { get; set; }
        }

        internal static object Play(Request request)
        {
            KeyboardDefinitions definition = ReadDefinition();
            if (definition == null)
                throw new InvalidOperationException("The native keyboard definition is missing.");

            var settings = XSettingsManager.Instance.Settings;
            // Match PlayDeviceHaptics: it scales once before HapticsManager applies its own strength.
            ushort duration = (ushort)(definition.keyHaptic * settings.HapticsStrength);
            try
            {
                PlayHaptics(request.HapticTarget, duration);
            }
            catch (Exception exception)
            {
                HostLog.Warn("Return haptics failed: " + exception.Message);
            }

            KeyLayout group = request.Numpad
                ? definition.keyboardGroups.numpadGroup
                : definition.keyboardGroups.mainGroup;
            var rows = GetActiveRows(group, KeyboardHelper.GetHklFormat());
            IntPtr hkl = KeyboardHelper.GetCurrentHkl();
            KeyboardKey key = rows.SelectMany(row => row)
                .First(item => ResolveKeycode(item.keycode, hkl, out var code, out _) && code == VirtualKeyCode.RETURN);
            string soundRoot = Path.Combine(Application.streamingAssetsPath, "Plugins", "KeyboardSounds");
            string[] packs = Directory.Exists(soundRoot)
                ? Directory.GetDirectories(soundRoot).Select(Path.GetFileName).OrderBy(name => name).ToArray()
                : Array.Empty<string>();
            int pack = settings.KeyboardSoundpackIndex;
            string soundPack = packs.Length == 0 ? null : packs[pack >= 0 && pack < packs.Length ? pack : 0];
            return new
            {
                soundPack,
                audio = key.audio,
                enabled = settings.KeyboardTypingSounds,
            };
        }

        private static void PlayHaptics(string hapticTarget, ushort duration)
        {
            Raycaster.DeviceIdxFetch device = Overlay_Manager
                .Instance
                .Keyboard_Overlay
                .OverlayWebView
                .LastInteractingDevice;
            if (
                hapticTarget == "leftcontroller"
                || hapticTarget == "keyboard" && device == Raycaster.DeviceIdxFetch.LeftHand
            )
                HapticsManager.Haptics(OVR_Pose_Handler.instance.leftIndex, 0, duration);
            else if (
                hapticTarget == "rightcontroller"
                || hapticTarget == "keyboard" && device == Raycaster.DeviceIdxFetch.RightHand
            )
                HapticsManager.Haptics(OVR_Pose_Handler.instance.rightIndex, 0, duration);
        }
    }
}
