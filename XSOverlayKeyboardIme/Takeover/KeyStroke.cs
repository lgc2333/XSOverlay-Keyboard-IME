using XSOverlayKeyboardIme.Input;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>One key the composition host receives: a typed character or a named key.</summary>
    internal readonly struct KeyStroke
    {
        private KeyStroke(char text, VirtualKey key, bool shift, bool isText)
        {
            Text = text;
            Key = key;
            Shift = shift;
            IsText = isText;
        }

        internal bool IsText { get; }

        internal char Text { get; }

        internal VirtualKey Key { get; }

        internal bool Shift { get; }

        /// <summary>Space and Enter commit the composition and end the session. See ADR 0007.</summary>
        internal bool EndsComposition
        {
            get { return !IsText && (Key == VirtualKey.Space || Key == VirtualKey.Return); }
        }

        internal static KeyStroke FromText(char text)
        {
            return new KeyStroke(text, default, false, true);
        }

        internal static KeyStroke FromKey(VirtualKey key, bool shift)
        {
            return new KeyStroke(default, key, shift, false);
        }

        internal string Describe()
        {
            return IsText ? "'" + Text + "'" : Key + (Shift ? " shift" : string.Empty);
        }
    }
}
