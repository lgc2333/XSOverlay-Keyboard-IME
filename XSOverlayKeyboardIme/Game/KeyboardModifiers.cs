using System;
using HarmonyLib;
using Vuplex.WebView;
using XSOverlay;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>Reads XSOverlay's live virtual and physical modifier state. See ADR 0040.</summary>
    internal static class KeyboardModifiers
    {
        private static readonly Func<InputManager, KeyModifier> ReadModifiers = AccessTools.MethodDelegate<
            Func<InputManager, KeyModifier>
        >(AccessTools.Method(typeof(InputManager), "GetWebModifiers"));

        internal static PageModifiers Read(InputManager input)
        {
            KeyModifier held = ReadModifiers(input);
            PageModifiers modifiers = PageModifiers.None;
            if ((held & KeyModifier.Shift) != 0)
                modifiers |= PageModifiers.Shift;
            if ((held & KeyModifier.Control) != 0)
                modifiers |= PageModifiers.Control;
            if ((held & KeyModifier.Alt) != 0)
                modifiers |= PageModifiers.Alt;
            if ((held & KeyModifier.Meta) != 0)
                modifiers |= PageModifiers.Meta;
            return modifiers;
        }
    }
}
