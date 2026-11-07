using System;
using System.Globalization;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The page's messages, parsed where the test project can reach them. See ADR 0005.</summary>
    internal static class PageMessages
    {
        /// <summary>The page asks for the IME at this index: the prefix, then the number.</summary>
        internal const string ProfilePrefix = "xso-keyboard-ime profile ";

        /// <summary>
        /// Reads the index off a profile request. Anything but one whole non-negative number
        /// is refused, so a garbled message cannot switch to an IME nobody picked. See ADR 0039.
        /// </summary>
        internal static bool TryReadProfileIndex(string message, out int index)
        {
            index = -1;
            if (message == null || !message.StartsWith(ProfilePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string number = message.Substring(ProfilePrefix.Length);
            if (number.Length == 0)
            {
                return false;
            }

            int parsed;
            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) || parsed < 0)
            {
                return false;
            }

            index = parsed;
            return true;
        }
    }
}
