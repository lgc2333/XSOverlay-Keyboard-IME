namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>How much of the keyboard the plugin owns right now. See ADR 0007 and ADR 0026.</summary>
    internal enum TakeoverGate
    {
        /// <summary>The IME composes nothing, so only a key that can start a composition is taken.</summary>
        Idle,

        /// <summary>The IME composes, so every key belongs to it.</summary>
        Composing,
    }
}
