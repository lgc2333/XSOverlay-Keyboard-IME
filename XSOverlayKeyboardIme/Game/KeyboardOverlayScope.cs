using XSOverlay;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>
    /// Decides whether the keyboard overlay's own composer owns the keyboard focus.
    /// Nothing else is taken over. See ADR 0007.
    /// </summary>
    internal static class KeyboardOverlayScope
    {
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
