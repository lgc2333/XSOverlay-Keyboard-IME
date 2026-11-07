namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// Who answers one press the keyboard page reported. The page, the IME, the system,
    /// or nobody. See ADR 0036.
    /// </summary>
    internal enum KeyOwnership
    {
        /// <summary>The page edits the composer itself, as it always would.</summary>
        Page,

        /// <summary>The plugin keeps the key and feeds nothing, so the page cannot act on it.</summary>
        Swallow,

        /// <summary>The composition host receives the key.</summary>
        Ime,

        /// <summary>The system gets its own combination, which here means the language switch.</summary>
        System,
    }
}
