namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// How the keyboard page delivered one press. A sticky or toggle key is a modifier the
    /// page latched rather than a character. See ADR 0036.
    /// </summary>
    internal enum PageKeyStyle
    {
        /// <summary>One complete press of a key that types.</summary>
        Standard,

        /// <summary>A modifier the page holds down until it is pressed again.</summary>
        Sticky,

        /// <summary>A lock key the page flips.</summary>
        Toggle,

        /// <summary>A press this plugin does not model, such as a macro.</summary>
        Other,
    }
}
