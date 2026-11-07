using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using XSOverlay;

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
            KeyLayout group = request.Numpad
                ? definition.keyboardGroups.numpadGroup
                : definition.keyboardGroups.mainGroup;
            var rows = GetActiveRows(group, KeyboardHelper.GetHklFormat());
            KeyboardKey key = rows.SelectMany(row => row).First(item => item.keycode == "RETURN");
            var settings = XSettingsManager.Instance.Settings;
            string soundRoot = Path.Combine(Application.streamingAssetsPath, "Plugins", "KeyboardSounds");
            string[] packs = Directory.Exists(soundRoot)
                ? Directory.GetDirectories(soundRoot).Select(Path.GetFileName).OrderBy(name => name).ToArray()
                : Array.Empty<string>();
            int pack = settings.KeyboardSoundpackIndex;
            string soundPack = packs.Length == 0 ? null : packs[pack >= 0 && pack < packs.Length ? pack : 0];

            // Match PlayDeviceHaptics: it scales once before HapticsManager applies its own strength.
            ushort duration = (ushort)(definition.keyHaptic * settings.HapticsStrength);
            Raycaster.DeviceIdxFetch device = Overlay_Manager
                .Instance
                .Keyboard_Overlay
                .OverlayWebView
                .LastInteractingDevice;
            if (
                request.HapticTarget == "leftcontroller"
                || request.HapticTarget == "keyboard" && device == Raycaster.DeviceIdxFetch.LeftHand
            )
                HapticsManager.Haptics(OVR_Pose_Handler.instance.leftIndex, 0, duration);
            else if (
                request.HapticTarget == "rightcontroller"
                || request.HapticTarget == "keyboard" && device == Raycaster.DeviceIdxFetch.RightHand
            )
                HapticsManager.Haptics(OVR_Pose_Handler.instance.rightIndex, 0, duration);
            return new
            {
                soundPack,
                audio = key.audio,
                enabled = settings.KeyboardTypingSounds,
            };
        }
    }
}
