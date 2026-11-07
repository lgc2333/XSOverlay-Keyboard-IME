using System;
using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// Decides who answers one press of a page key, and what the IME receives when it does.
    /// See ADR 0026, ADR 0029 and ADR 0036.
    /// </summary>
    internal static class KeyStrokeReader
    {
        // The page names keys the way LSHIFT and RSHIFT are named, and its exact spelling
        // is undocumented, so the families are matched by token. See ADR 0029.
        private static readonly string[] ShiftMarkers = { "SHIFT" };
        private static readonly string[] ControlMarkers = { "CTRL", "CONTROL" };
        private static readonly string[] AltMarkers = { "ALT" };
        private static readonly string[] MetaMarkers = { "WIN", "META" };
        private static readonly string[] SpaceMarkers = { "SPACE" };

        /// <summary>
        /// Reads one press. The strokes list holds what the IME receives; a decision that
        /// leaves the list empty still means the plugin keeps the key when it says so.
        /// </summary>
        internal static KeyOwnership Read(PageKey key, TakeoverGate gate, List<KeyStroke> strokes)
        {
            strokes.Clear();

            if (key.Style != PageKeyStyle.Standard)
            {
                return KeyOwnership.Page;
            }

            // The language switch reaches our own IME, so Windows must get it. See ADR 0036.
            if (IsSystemSwitch(key))
            {
                strokes.Add(KeyStroke.FromSystem());
                return KeyOwnership.System;
            }

            // Every other Win combination stays off the page without feeding anything.
            if (HoldsMeta(key))
            {
                return KeyOwnership.Swallow;
            }

            if (gate == TakeoverGate.Composing)
            {
                return ReadComposing(key, strokes);
            }

            return ReadIdle(key, strokes);
        }

        /// <summary>
        /// While the IME composes it owns the keyboard: a name it cannot use is dropped
        /// rather than handed back to the page.
        /// </summary>
        private static KeyOwnership ReadComposing(PageKey key, List<KeyStroke> strokes)
        {
            // Every other modifier means the page interprets the key. See ADR 0029.
            if (HoldsCombination(key))
            {
                return KeyOwnership.Page;
            }

            string text = key.Text;
            if (text.Length == 1 && text[0] != ' ')
            {
                strokes.Add(KeyStroke.FromText(text[0], key.NativeKey, HasShift(key)));
                return KeyOwnership.Ime;
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
                    strokes.Clear();
                    return KeyOwnership.Swallow;
                }

                strokes.Add(KeyStroke.FromKey(mapped, shift));
            }

            // A page that reports no key name for the space still commits with it.
            if (strokes.Count == 0 && text == " ")
            {
                strokes.Add(KeyStroke.FromKey(VirtualKey.Space, false));
            }

            return strokes.Count > 0 ? KeyOwnership.Ime : KeyOwnership.Swallow;
        }

        /// <summary>While the IME composes nothing, only a key that can start it is taken.</summary>
        private static KeyOwnership ReadIdle(PageKey key, List<KeyStroke> strokes)
        {
            // Every other modifier means the page interprets the key. See ADR 0029.
            if (HoldsCombination(key))
            {
                return KeyOwnership.Page;
            }

            // The page already resolved the character, including the shift state. A space
            // never starts a composition, so it stays with the page. A lone modifier has no
            // character, so it falls through to the page like every other key.
            string text = key.Text;
            if (text.Length == 1 && text[0] != ' ')
            {
                strokes.Add(KeyStroke.FromText(text[0], key.NativeKey, HasShift(key)));
                return KeyOwnership.Ime;
            }

            return KeyOwnership.Page;
        }

        /// <summary>Win+Space is the one combination that belongs to the system. See ADR 0036.</summary>
        private static bool IsSystemSwitch(PageKey key)
        {
            if (!HoldsMeta(key))
            {
                return false;
            }

            return key.Text == " " || HasAnyMarker(key.Names, SpaceMarkers);
        }

        private static bool HasShift(PageKey key) =>
            (key.Modifiers & PageModifiers.Shift) != 0 || HasAnyMarker(key.Names, ShiftMarkers);

        private static bool HoldsMeta(PageKey key)
        {
            return (key.Modifiers & PageModifiers.Meta) != 0 || HasAnyMarker(key.Names, MetaMarkers);
        }

        private static bool HoldsCombination(PageKey key)
        {
            return (key.Modifiers & (PageModifiers.Control | PageModifiers.Alt)) != 0
                || HasAnyMarker(key.Names, ControlMarkers)
                || HasAnyMarker(key.Names, AltMarkers);
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
