using System;
using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// Turns one page key into strokes for the composition host, as far as the current
    /// gate allows. See ADR 0007 and ADR 0026.
    /// </summary>
    internal static class KeyStrokeReader
    {
        // The page names keys the way LSHIFT and RSHIFT are named, and its exact spelling
        // is undocumented, so the families are matched by token. See ADR 0029.
        private static readonly string[] ShiftMarkers = { "SHIFT" };
        private static readonly string[] CombinationMarkers = { "CTRL", "CONTROL", "ALT" };
        private static readonly string[] MetaMarkers = { "WIN", "META" };

        internal static bool TryRead(PageKey key, TakeoverGate gate, List<KeyStroke> strokes)
        {
            strokes.Clear();
            if (!key.IsSinglePress)
            {
                return false;
            }

            // A press that carries only a modifier belongs to the page: the game has to
            // record the key before a combination with it can be recognized. See ADR 0029.
            if (IsModifierOnly(key))
            {
                return false;
            }

            // Windows owns every Win combination, so the page must not type its character.
            // Taking the key with nothing to feed is what keeps it off the page. See ADR 0029.
            if (HoldsMeta(key))
            {
                return true;
            }

            // Every other modifier means the page interprets the key. See ADR 0029.
            if (HoldsCombination(key))
            {
                return false;
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

            bool shift = HasAnyMarker(key.Names, ShiftMarkers) || (key.Modifiers & PageModifiers.Shift) != 0;
            foreach (string name in key.Names)
            {
                if (HasMarker(name, ShiftMarkers))
                {
                    continue;
                }

                if (!TryMapNamedKey(name, out VirtualKey mapped))
                {
                    // The IME composes, so the page must not edit the composer under it. A
                    // name that cannot be translated is dropped instead of handed on.
                    strokes.Clear();
                    return true;
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

        private static bool IsModifierOnly(PageKey key)
        {
            if (key.Text.Length > 0 || key.Names.Length == 0)
            {
                return false;
            }

            foreach (string name in key.Names)
            {
                if (
                    !HasMarker(name, ShiftMarkers)
                    && !HasMarker(name, CombinationMarkers)
                    && !HasMarker(name, MetaMarkers)
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HoldsMeta(PageKey key)
        {
            return (key.Modifiers & PageModifiers.Meta) != 0 || HasAnyMarker(key.Names, MetaMarkers);
        }

        private static bool HoldsCombination(PageKey key)
        {
            return (key.Modifiers & (PageModifiers.Control | PageModifiers.Alt)) != 0
                || HasAnyMarker(key.Names, CombinationMarkers);
        }

        private static bool HasAnyMarker(string[] names, string[] markers)
        {
            foreach (string name in names)
            {
                if (HasMarker(name, markers))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMarker(string name, string[] markers)
        {
            if (name == null)
            {
                return false;
            }

            foreach (string marker in markers)
            {
                if (name.Contains(marker, StringComparison.OrdinalIgnoreCase))
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
