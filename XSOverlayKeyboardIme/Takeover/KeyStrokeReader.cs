using System;
using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// Turns a keyboard page event into strokes for the composition host. Named keys
    /// beyond text only matter while composing, so the caller decides whether they
    /// are taken. See ADR 0007.
    /// </summary>
    internal static class KeyStrokeReader
    {
        private static readonly string[] ModifierNames = { "LSHIFT", "RSHIFT" };

        internal static bool TryRead(
            XSOverlay.Websockets.API.Objects.KeyboardEvent keyEvent,
            bool includeNamedKeys,
            List<KeyStroke> strokes
        )
        {
            strokes.Clear();
            if (keyEvent == null || keyEvent.keyPressStyle != XSOverlay.Websockets.API.Enums.KeyPressStyle.Standard)
            {
                return false;
            }

            // The page already resolved the character, including the shift state. A space
            // never starts a composition, so while idle it stays with the page.
            string text = keyEvent.webValue;
            if (text != null && text.Length == 1 && text[0] != ' ')
            {
                strokes.Add(KeyStroke.FromText(text[0]));
                return true;
            }

            if (!includeNamedKeys)
            {
                return false;
            }

            bool shift = false;
            string[] values = keyEvent.value ?? Array.Empty<string>();
            foreach (string name in values)
            {
                if (IsModifier(name))
                {
                    shift = true;
                }
            }

            foreach (string name in values)
            {
                if (IsModifier(name))
                {
                    continue;
                }

                if (!TryMapNamedKey(name, out VirtualKey key))
                {
                    strokes.Clear();
                    return false;
                }

                strokes.Add(KeyStroke.FromKey(key, shift));
            }

            // A page that reports no key name for the space still commits with it.
            if (strokes.Count == 0 && text == " ")
            {
                strokes.Add(KeyStroke.FromKey(VirtualKey.Space, false));
            }

            return strokes.Count > 0;
        }

        private static bool IsModifier(string name)
        {
            foreach (string modifier in ModifierNames)
            {
                if (string.Equals(name, modifier, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryMapNamedKey(string name, out VirtualKey key)
        {
            switch ((name ?? string.Empty).ToUpperInvariant())
            {
                case "BACK":
                    key = VirtualKey.Back;
                    return true;
                case "TAB":
                    key = VirtualKey.Tab;
                    return true;
                case "RETURN":
                    key = VirtualKey.Return;
                    return true;
                case "ESCAPE":
                    key = VirtualKey.Escape;
                    return true;
                case "SPACE":
                    key = VirtualKey.Space;
                    return true;
                case "PRIOR":
                    key = VirtualKey.PageUp;
                    return true;
                case "NEXT":
                    key = VirtualKey.PageDown;
                    return true;
                case "END":
                    key = VirtualKey.End;
                    return true;
                case "HOME":
                    key = VirtualKey.Home;
                    return true;
                case "LEFT":
                    key = VirtualKey.Left;
                    return true;
                case "UP":
                    key = VirtualKey.Up;
                    return true;
                case "RIGHT":
                    key = VirtualKey.Right;
                    return true;
                case "DOWN":
                    key = VirtualKey.Down;
                    return true;
                case "DELETE":
                    key = VirtualKey.Delete;
                    return true;
                default:
                    key = default;
                    return false;
            }
        }
    }
}
