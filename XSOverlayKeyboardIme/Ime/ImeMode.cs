namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>What the IME does with the next key. See ADR 0025.</summary>
    internal enum ImeMode
    {
        Unknown = -1,

        /// <summary>Keys enter a composition. The IME reports this value as AlphaNumeric.</summary>
        Composing = 0,

        /// <summary>Keys pass through as plain characters. The IME reports this value as Native.</summary>
        Passthrough = 1,
    }

    /// <summary>
    /// Maps the value IngameIME reports to what the code acts on. The labels run
    /// backwards: measured, AlphaNumeric composes and Native passes keys through, so
    /// neither name can be trusted. See ADR 0025.
    /// </summary>
    internal static class ImeModeMap
    {
        internal static ImeMode FromShim(int value)
        {
            if (value == 0)
            {
                return ImeMode.Composing;
            }

            if (value == 1)
            {
                return ImeMode.Passthrough;
            }

            return ImeMode.Unknown;
        }
    }
}
