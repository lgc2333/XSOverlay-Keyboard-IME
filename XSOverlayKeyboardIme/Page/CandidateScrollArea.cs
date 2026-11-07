using System;
using Newtonsoft.Json;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>Normalized page bounds for ray browsing. See ADR 0043.</summary>
    internal sealed class CandidateScrollArea
    {
        internal const string MessagePrefix = "xso-keyboard-ime candidate-area ";

        public float Left { get; set; }
        public float Top { get; set; }
        public float Right { get; set; }
        public float Bottom { get; set; }
        public float ScrollX { get; set; }
        public float ScrollY { get; set; }

        internal bool Contains(float x, float y) => x >= Left && x <= Right && y >= Top && y <= Bottom;

        internal static CandidateScrollArea Read(string json)
        {
            CandidateScrollArea area = JsonConvert.DeserializeObject<CandidateScrollArea>(json);
            return
                area != null
                && Finite(area.Left)
                && Finite(area.Top)
                && Finite(area.Right)
                && Finite(area.Bottom)
                && Finite(area.ScrollX)
                && Finite(area.ScrollY)
                && area.Right > area.Left
                && area.Bottom > area.Top
                && area.ScrollX >= 0
                && area.ScrollX <= 1
                && area.ScrollY >= 0
                && area.ScrollY <= 1
                ? area
                : null;
        }

        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);
    }
}
