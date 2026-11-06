using System;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>
    /// The modifiers held when the page reported a key. Shift goes into the stroke;
    /// Control, Alt and Meta decide where the key goes. See ADR 0029.
    /// </summary>
    [Flags]
    internal enum PageModifiers
    {
        None = 0,
        Shift = 1,
        Control = 2,
        Alt = 4,
        Meta = 8,
    }
}
