using System;
using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// Turns one page key into strokes for the composition host, as far as the current
    /// gate allows. See ADR 0007.
    /// </summary>
    internal static class KeyStrokeReader
    {
        private static readonly string[] ModifierNames = { "LSHIFT", "RSHIFT" };

        internal static bool TryRead(PageKey key, TakeoverGate gate, List<KeyStroke> strokes)
        {
            strokes.Clear();
            if (!key.IsSinglePress)
            {
                return false;
            }

            // The IME is passing keys through, so the page keeps every key but the one
            // that ends the session. See ADR 0025.
            if (gate == TakeoverGate.Session)
            {
                return TryReadSubmitKey(key, strokes);
            }

            // The page already resolved the character, including the shift state. A space
            // never starts a composition, so while idle it stays with the page.
            string text = key.Text;
            if (text.Length == 1 && text[0] != ' ')
            {
                strokes.Add(KeyStroke.FromText(text[0]));
                return true;
            }

            if (gate == TakeoverGate.Idle)
            {
                return false;
            }

            bool shift = HasShift(key.Names);
            foreach (string name in key.Names)
            {
                if (IsModifier(name))
                {
                    continue;
                }

                if (!TryMapNamedKey(name, out VirtualKey mapped))
                {
                    strokes.Clear();
                    return false;
                }

                strokes.Add(KeyStroke.FromKey(mapped, shift));
            }

            // A page that reports no key name for the space still commits with it.
            if (strokes.Count == 0 && text == " ")
            {
                strokes.Add(KeyStroke.FromKey(VirtualKey.Space, false));
            }

            return strokes.Count > 0;
        }

        /// <summary>Space and Enter end a session even when the IME composes nothing.</summary>
        private static bool TryReadSubmitKey(PageKey key, List<KeyStroke> strokes)
        {
            if (key.Text == " ")
            {
                strokes.Add(KeyStroke.FromKey(VirtualKey.Space, false));
                return true;
            }

            foreach (string name in key.Names)
            {
                if (string.Equals(name, "RETURN", StringComparison.OrdinalIgnoreCase))
                {
                    strokes.Add(KeyStroke.FromKey(VirtualKey.Return, false));
                    return true;
                }
            }

            return false;
        }

        private static bool HasShift(string[] names)
        {
            foreach (string name in names)
            {
                if (IsModifier(name))
                {
                    return true;
                }
            }

            return false;
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
