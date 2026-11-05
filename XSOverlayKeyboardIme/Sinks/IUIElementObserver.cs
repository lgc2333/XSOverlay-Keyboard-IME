namespace XSOverlayKeyboardIme.Sinks
{
    /// <summary>Receives IME UI element events. Implementations must return at once.</summary>
    internal interface IUIElementObserver
    {
        void ElementBegun(uint elementId);

        void ElementUpdated(uint elementId);

        void ElementEnded(uint elementId);
    }
}
