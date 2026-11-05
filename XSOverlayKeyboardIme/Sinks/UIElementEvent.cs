namespace XSOverlayKeyboardIme.Sinks
{
    /// <summary>What the IME reported about its UI element.</summary>
    internal enum UIElementEventKind
    {
        Begun,
        Updated,
        Ended,
    }

    /// <summary>One observed event, queued for the host thread.</summary>
    internal readonly struct UIElementEvent
    {
        internal UIElementEvent(UIElementEventKind kind, uint elementId)
        {
            Kind = kind;
            ElementId = elementId;
        }

        internal UIElementEventKind Kind { get; }

        internal uint ElementId { get; }
    }
}
