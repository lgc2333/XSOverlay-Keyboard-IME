using Vuplex.WebView;
using XSOverlay;
using XSOverlay.WebApp;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>
    /// Decides whether the keyboard overlay's own composer owns the keyboard focus.
    /// Nothing else is taken over. See ADR 0007.
    /// </summary>
    internal static class KeyboardOverlayScope
    {
        /// <summary>The game's overlay registry, or null before the keyboard overlay exists.</summary>
        internal static Overlay_Manager KeyboardOverlays()
        {
            Overlay_Manager overlays = Overlay_Manager.Instance;
            return overlays == null || overlays.Keyboard_Overlay == null ? null : overlays;
        }

        /// <summary>Reports every input of the decision, so a refusal can be explained.</summary>
        internal static string Describe()
        {
            InputManager input = InputManager.Instance;
            Overlay_Manager overlays = KeyboardOverlays();
            KeyboardInputFocusManager focus = input == null ? null : input.KeyboardFocus;
            ulong keyboard = overlays == null ? 0 : overlays.Keyboard_Overlay.handle;
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

        /// <summary>The keyboard overlay's page, or null before the game creates it.</summary>
        internal static IWebView FindPage()
        {
            Overlay_Manager overlays = KeyboardOverlays();
            if (overlays == null)
            {
                return null;
            }

            OverlayWebView page;
            if (!overlays.WebViewsOverlays.TryGetValue(overlays.Keyboard_Overlay.handle, out page) || page == null)
            {
                return null;
            }

            return page.CurrentWebView;
        }

        internal static bool TryGetFocusedOverlay(out KeyboardInputFocusManager focus)
        {
            focus = null;

            InputManager input = InputManager.Instance;
            Overlay_Manager overlays = KeyboardOverlays();
            if (input == null || overlays == null)
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
