using XSOverlayKeyboardIme.Input;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>What one stroke carries. See ADR 0036.</summary>
    internal enum KeyStrokeKind
    {
        /// <summary>A character the page already resolved.</summary>
        Text,

        /// <summary>A named key, optionally with shift held.</summary>
        Key,

        /// <summary>The system's own language switch, Win+Space.</summary>
        System,
    }

    /// <summary>One key the composition host receives: a typed character or a named key.</summary>
    internal readonly struct KeyStroke
    {
        private KeyStroke(char text, VirtualKey key, bool shift, KeyStrokeKind kind)
        {
            Text = text;
            Key = key;
            Shift = shift;
            Kind = kind;
        }

        internal KeyStrokeKind Kind { get; }

        internal char Text { get; }

        internal VirtualKey Key { get; }

        internal bool Shift { get; }

        internal static KeyStroke FromText(char text)
        {
            return new KeyStroke(text, default, false, KeyStrokeKind.Text);
        }

        internal static KeyStroke FromKey(VirtualKey key, bool shift)
        {
            return new KeyStroke(default, key, shift, KeyStrokeKind.Key);
        }

        /// <summary>The language switch belongs to Windows, so it is a chord, not a key.</summary>
        internal static KeyStroke FromSystem()
        {
            return new KeyStroke(default, default, false, KeyStrokeKind.System);
        }

        internal string Describe()
        {
            switch (Kind)
            {
                case KeyStrokeKind.Text:
                    return "'" + Text + "'";
                case KeyStrokeKind.System:
                    return "Win+Space";
                default:
                    return Key + (Shift ? " shift" : string.Empty);
            }
        }
    }
}
