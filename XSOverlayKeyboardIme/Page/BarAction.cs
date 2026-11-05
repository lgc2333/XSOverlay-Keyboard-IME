using System;
using System.Globalization;

namespace XSOverlayKeyboardIme.Page
{
    internal enum BarActionKind
    {
        Ready,
        Pick,
        Turn,
    }

    /// <summary>
    /// One action the candidate bar sends back, written as the single line
    /// <c>xso-keyboard-ime &lt;verb&gt; [argument]</c>. See ADR 0004 and ADR 0010.
    /// </summary>
    internal readonly struct BarAction
    {
        private const string Prefix = "xso-keyboard-ime";
        private static readonly char[] Separators = { ' ' };

        private BarAction(BarActionKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }

        internal BarActionKind Kind { get; }

        /// <summary>The candidate index of a pick, or the direction of a turn.</summary>
        internal int Value { get; }

        /// <summary>Reads one bar line; false means the plugin must ignore it.</summary>
        internal static bool TryParse(string message, out BarAction action)
        {
            action = default;
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            string[] parts = message.Trim().Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            switch (parts[1].ToLowerInvariant())
            {
                case "ready":
                    action = new BarAction(BarActionKind.Ready, 0);
                    return parts.Length == 2;
                case "pick":
                    if (!TryReadValue(parts, out int index) || index < 0)
                    {
                        return false;
                    }

                    action = new BarAction(BarActionKind.Pick, index);
                    return true;
                case "turn":
                    if (!TryReadValue(parts, out int delta) || delta == 0)
                    {
                        return false;
                    }

                    action = new BarAction(BarActionKind.Turn, delta);
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryReadValue(string[] parts, out int value)
        {
            value = 0;
            return parts.Length == 3
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
