namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Warns once when a streak of compositions reports no text. See ADR 0013.</summary>
    internal sealed class InlinePreeditWatch
    {
        private readonly int _missesBeforeWarning;
        private int _misses;
        private bool _warned;

        internal InlinePreeditWatch(int missesBeforeWarning = 2)
        {
            _missesBeforeWarning = missesBeforeWarning;
        }

        /// <summary>Reports whether this observation closes a silent streak.</summary>
        internal bool Observe(bool hasCompositionText)
        {
            if (hasCompositionText)
            {
                _misses = 0;
                _warned = false;
                return false;
            }

            _misses++;
            if (_misses >= _missesBeforeWarning && !_warned)
            {
                _warned = true;
                return true;
            }

            return false;
        }
    }
}
