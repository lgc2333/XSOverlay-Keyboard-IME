using System;

namespace XSOverlayKeyboardIme.Chatbox
{
    /// <summary>Coalesces committed composer text into silent OSC updates.</summary>
    internal sealed class ChatboxBroadcast
    {
        private readonly Action<string, object[]> _send;
        private bool _active;
        private bool _submitting;
        private bool _nativeSubmission;
        private bool _typing;
        private string _text = string.Empty;
        private string _sentText = string.Empty;
        private long? _lastSent;
        internal int Revision { get; private set; }

        internal ChatboxBroadcast(Action<string, object[]> send)
        {
            _send = send;
        }

        internal void Update(bool active, string text, bool submitting, int revision = 0)
        {
            if (_nativeSubmission || revision != Revision)
                return;
            _active = active;
            _text = Preview(text ?? string.Empty);
            _submitting = submitting;
            if (!active || _text.Length == 0)
                _sentText = string.Empty;
        }

        internal void BeginSubmission()
        {
            Revision++;
            _nativeSubmission = true;
            _active = false;
            _text = string.Empty;
        }

        internal void EndSubmission(long nowMilliseconds)
        {
            _nativeSubmission = false;
            _lastSent = nowMilliseconds;
            _sentText = string.Empty;
        }

        internal void ResetDelivery()
        {
            _typing = false;
            _sentText = string.Empty;
        }

        internal void Pump(long nowMilliseconds)
        {
            bool typing = _active && !_submitting;
            bool typingChanged = _typing != typing;
            if (typingChanged)
            {
                _typing = typing;
                _send("/chatbox/typing", new object[] { typing });
            }
            if (!typing || _text.Length == 0 || _text == _sentText)
                return;
            if (_lastSent.HasValue && nowMilliseconds - _lastSent.Value < 1000)
                return;

            _lastSent = nowMilliseconds;
            _sentText = _text;
            _send("/chatbox/input", new object[] { _text, true, false });
            if (!typingChanged)
                _send("/chatbox/typing", new object[] { true });
        }

        private static string Preview(string text)
        {
            if (text.Length <= 144)
                return text;
            int length = char.IsHighSurrogate(text[143]) && char.IsLowSurrogate(text[144]) ? 143 : 144;
            return text.Substring(0, length);
        }
    }
}
