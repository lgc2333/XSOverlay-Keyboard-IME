namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>Wire envelope captured at page event entry, not at native receipt.</summary>
    internal sealed class FocusMessage
    {
        public string Kind { get; set; }
        public string Document { get; set; }
        public long Revision { get; set; }
        public long Request { get; set; }
        public int Index { get; set; }
        internal FocusTicket Ticket => new FocusTicket(Document, Revision, Request);
    }
}
