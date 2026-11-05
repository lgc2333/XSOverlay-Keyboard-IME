using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Composition
{
    /// <summary>Keys that pick and turn candidates through the IME's own bindings. See ADR 0010.</summary>
    internal static class CandidateKeys
    {
        /// <summary>Digits reach the first nine candidates; past them the arrows still work.</summary>
        private const int DigitCandidates = 9;

        /// <summary>Adds the keys that select one candidate of the page in front of the IME.</summary>
        internal static bool ForPick(int index, bool digitPicks, List<KeyStroke> strokes)
        {
            strokes.Clear();
            if (index < 0)
            {
                return false;
            }

            if (digitPicks && index < DigitCandidates)
            {
                strokes.Add(KeyStroke.FromText((char)('1' + index)));
                return true;
            }

            for (int step = 0; step < index; step++)
            {
                strokes.Add(KeyStroke.FromKey(VirtualKey.Down, false));
            }

            strokes.Add(KeyStroke.FromKey(VirtualKey.Return, false));
            return true;
        }

        /// <summary>Adds the keys that turn to the next or the previous candidate page.</summary>
        internal static void ForTurn(int delta, List<KeyStroke> strokes)
        {
            strokes.Clear();
            strokes.Add(KeyStroke.FromKey(delta < 0 ? VirtualKey.PageUp : VirtualKey.PageDown, false));
        }
    }
}
