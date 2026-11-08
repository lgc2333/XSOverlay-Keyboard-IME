namespace XSOverlayKeyboardIme.Chatbox
{
    internal sealed class ChatboxState
    {
        public int Revision { get; set; }
        public bool Active { get; set; }
        public string Text { get; set; }
        public bool Submitting { get; set; }
    }
}
