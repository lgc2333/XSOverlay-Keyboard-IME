using XSOverlay;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>
    /// Decides whether the keyboard overlay's own composer owns the keyboard focus.
    /// Nothing else is taken over. See ADR 0007.
    /// </summary>
    internal static class KeyboardOverlayScope
    {
        /// <summary>Reports every input of the decision, so a refusal can be explained.</summary>
        internal static string Describe()
        {
            InputManager input = InputManager.Instance;
            Overlay_Manager overlays = Overlay_Manager.Instance;
            KeyboardInputFocusManager focus = input == null ? null : input.KeyboardFocus;
            ulong keyboard =
                overlays == null || overlays.Keyboard_Overlay == null ? 0 : overlays.Keyboard_Overlay.handle;
            ulong owner = focus == null ? 0 : focus.OwnerHandle;

            return "hasFocus="
                + (focus != null && focus.HasFocus)
                + " owner=0x"
                + owner.ToString("x")
                + " keyboard=0x"
                + keyboard.ToString("x")
                + " keyOverlayKnown="
                + (keyboard != 0)
                + " webView="
                + (overlays != null && overlays.WebViewsOverlays.ContainsKey(owner));
        }

        internal static bool TryGetFocusedOverlay(out KeyboardInputFocusManager focus)
        {
            focus = null;

            InputManager input = InputManager.Instance;
            Overlay_Manager overlays = Overlay_Manager.Instance;
            if (input == null || overlays == null || overlays.Keyboard_Overlay == null)
            {
                return false;
            }

            KeyboardInputFocusManager candidate = input.KeyboardFocus;
            if (candidate == null || !candidate.HasFocus)
            {
                return false;
            }

            ulong owner = candidate.OwnerHandle;
            if (owner == 0 || owner != overlays.Keyboard_Overlay.handle)
            {
                return false;
            }

            focus = candidate;
            return true;
        }
    }
}
