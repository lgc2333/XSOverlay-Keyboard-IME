using System;

namespace XSOverlayKeyboardIme.Takeover
{
    internal static class ForegroundKeyState
    {
        private const int NlsAlphanumeric = 0xF0;
        private const int NlsHiragana = 0xF2;

        internal static bool IsIdle(Func<int, short> getAsyncKeyState)
        {
            for (int key = 1; key < 256; key++)
            {
                // NLS mode state can outlive the trigger key and block acquisition. See ADR 0063.
                if (key is >= NlsAlphanumeric and <= NlsHiragana)
                    continue;
                if ((getAsyncKeyState(key) & 0x8000) != 0)
                    return false;
            }
            return true;
        }
    }
}
