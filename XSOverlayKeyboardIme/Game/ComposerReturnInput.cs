using XSOverlay;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>Shares the ordinary key gate with Return paths consumed early by the page.</summary>
    internal static class ComposerReturnInput
    {
        internal static bool TryAccept(FocusTicket? source = null)
        {
            InputManager input = InputManager.Instance;
            ImeHost host = ImeHost.Current;
            if (input == null || host == null || !KeyboardOverlayScope.HasKeyboardFocus())
                return false;
            var key = new PageKey(
                PageKeyStyle.Standard,
                new[] { "RETURN" },
                "Enter",
                KeyboardModifiers.Read(input),
                VirtualKey.Return
            );
            return host.TryAcceptKey(key, () => KeyboardModifierInput.ConsumeSticky(input), source);
        }
    }
}
