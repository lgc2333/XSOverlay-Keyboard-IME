using XSOverlay;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>
    /// The modifiers held when the page reported a key: the game's polled state, which
    /// knows the keys the page toggles, and the physical keys, which the poll lags.
    /// See ADR 0029.
    /// </summary>
    internal static class KeyboardModifiers
    {
        internal static PageModifiers Read(InputManager input)
        {
            KeyboardModifierState state = input == null ? null : input.KeyboardState;

            bool shift =
                Down(VirtualKey.LeftShift, VirtualKey.RightShift)
                || (state != null && (state.leftShift || state.rightShift));
            bool control =
                Down(VirtualKey.LeftControl, VirtualKey.RightControl)
                || (state != null && (state.leftCtrl || state.rightCtrl));
            bool alt =
                Down(VirtualKey.LeftAlt, VirtualKey.RightAlt) || (state != null && (state.leftAlt || state.rightAlt));
            bool meta = Down(VirtualKey.LeftWin, VirtualKey.RightWin) || Holds(state, "LWIN", "RWIN");

            return From(shift, control, alt, meta);
        }

        private static PageModifiers From(bool shift, bool control, bool alt, bool meta)
        {
            PageModifiers modifiers = PageModifiers.None;
            if (shift)
            {
                modifiers |= PageModifiers.Shift;
            }

            if (control)
            {
                modifiers |= PageModifiers.Control;
            }

            if (alt)
            {
                modifiers |= PageModifiers.Alt;
            }

            if (meta)
            {
                modifiers |= PageModifiers.Meta;
            }

            return modifiers;
        }

        /// <summary>Whether either key of a modifier pair is down right now.</summary>
        private static bool Down(VirtualKey left, VirtualKey right)
        {
            return Held(left) || Held(right);
        }

        private static bool Held(VirtualKey key)
        {
            return (Win32Native.GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        private static bool Holds(KeyboardModifierState state, params string[] names)
        {
            if (state == null || state.activeKeys == null)
            {
                return false;
            }

            foreach (string name in names)
            {
                if (state.activeKeys.Contains(name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
