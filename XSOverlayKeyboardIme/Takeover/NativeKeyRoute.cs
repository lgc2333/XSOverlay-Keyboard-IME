namespace XSOverlayKeyboardIme.Takeover
{
    internal static class NativeKeyRoute
    {
        // Layout-produced text, including AltGr and dead keys, stays in TranslateMessage.
        internal static bool UsesBrowser(int key, bool eaten, bool shortcut, bool layoutText) =>
            !eaten && NativeKeyName.TryRead(key, out _) && (!NativeKeyName.IsCharacter(key) || shortcut && !layoutText);
    }
}
