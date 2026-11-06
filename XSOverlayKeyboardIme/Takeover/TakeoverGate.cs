namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>How much of the keyboard the plugin owns right now. See ADR 0007.</summary>
    internal enum TakeoverGate
    {
        /// <summary>No session runs, so only a text key that can start one is taken.</summary>
        Idle,

        /// <summary>A session runs but the IME passes keys through, so only a submit key is taken.</summary>
        Session,

        /// <summary>The IME composes, so every key belongs to it.</summary>
        Composing,
    }
}
