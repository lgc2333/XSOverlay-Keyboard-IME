using System;
using HarmonyLib;
using WindowsInput;
using WindowsInput.Native;
using XSOverlay;
using XSOverlay.Websockets.API;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>Uses XSOverlay's modifier transitions and release rules. See ADR 0040.</summary>
    internal static class KeyboardModifierInput
    {
        private static readonly Action<InputManager, bool> ReleaseSticky = AccessTools.MethodDelegate<
            Action<InputManager, bool>
        >(AccessTools.Method(typeof(InputManager), "ReleaseStickyKeys"));
        private static readonly AccessTools.FieldRef<
            InputManager,
            System.Collections.Generic.HashSet<VirtualKeyCode>
        > WebSticky = AccessTools.FieldRefAccess<InputManager, System.Collections.Generic.HashSet<VirtualKeyCode>>(
            "_webStickyKeys"
        );

        internal static bool UseWebFocus(bool hasFocus, Objects.KeyboardEvent key)
        {
            if (
                !hasFocus
                || !InScope()
                || key == null
                || key.keyPressStyle is not (Enums.KeyPressStyle.Sticky or Enums.KeyPressStyle.Toggle)
            )
            {
                return hasFocus;
            }

            return key.value == null
                || key.value.Length == 0
                || !Enum.TryParse(key.value[0], true, out VirtualKeyCode code)
                || !IsSystemModifier(code);
        }

        internal static bool IsSystemModifier(VirtualKeyCode key) =>
            key
                is VirtualKeyCode.SHIFT
                    or VirtualKeyCode.LSHIFT
                    or VirtualKeyCode.RSHIFT
                    or VirtualKeyCode.CONTROL
                    or VirtualKeyCode.LCONTROL
                    or VirtualKeyCode.RCONTROL
                    or VirtualKeyCode.MENU
                    or VirtualKeyCode.LMENU
                    or VirtualKeyCode.RMENU
                    or VirtualKeyCode.CAPITAL;

        internal static VirtualKey ReadNativeKey(string[] names)
        {
            if (names != null)
            {
                foreach (string name in names)
                {
                    if (
                        Enum.TryParse(name, true, out VirtualKeyCode code)
                        && !IsSystemModifier(code)
                        && code is not (VirtualKeyCode.LWIN or VirtualKeyCode.RWIN)
                    )
                    {
                        return (VirtualKey)code;
                    }
                }
            }

            return default;
        }

        internal static void ConsumeSticky(InputManager input)
        {
            ReleaseSticky(input, true);
            WebSticky(input).Clear();
        }

        internal static IKeyboardSimulator KeyDown(IKeyboardSimulator keyboard, VirtualKeyCode key)
        {
            return Forward(key, KeyStrokeKind.ModifierDown) ? keyboard : keyboard.KeyDown(key);
        }

        internal static IKeyboardSimulator KeyUp(IKeyboardSimulator keyboard, VirtualKeyCode key)
        {
            return Forward(key, KeyStrokeKind.ModifierUp) ? keyboard : keyboard.KeyUp(key);
        }

        internal static IKeyboardSimulator KeyPress(IKeyboardSimulator keyboard, VirtualKeyCode key)
        {
            return Forward(key, KeyStrokeKind.ModifierPress) ? keyboard : keyboard.KeyPress(key);
        }

        private static bool Forward(VirtualKeyCode key, KeyStrokeKind kind)
        {
            return IsSystemModifier(key)
                && InScope()
                && ImeHost.Current.TryPostModifier(KeyStroke.FromModifier((VirtualKey)key, kind));
        }

        private static bool InScope() => ImeHost.Current?.Ready == true && KeyboardOverlayScope.HasKeyboardFocus();
    }
}
