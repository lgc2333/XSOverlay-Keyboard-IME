using System.Text;

namespace XSOverlayKeyboardIme.Input
{
    /// <summary>Keeps UTF-16 pairs together when Windows delivers separate WM_CHAR messages.</summary>
    internal sealed class HostTextBuffer
    {
        private readonly StringBuilder _text = new StringBuilder();

        internal void Append(char character)
        {
            if (char.IsLowSurrogate(character))
            {
                if (_text.Length > 0 && char.IsHighSurrogate(_text[_text.Length - 1]))
                    _text.Append(character);
                return;
            }
            if (_text.Length > 0 && char.IsHighSurrogate(_text[_text.Length - 1]))
                _text.Length--;
            _text.Append(character);
        }

        internal string Take()
        {
            int length = _text.Length;
            if (length > 0 && char.IsHighSurrogate(_text[length - 1]))
                length--;
            string result = _text.ToString(0, length);
            _text.Remove(0, length);
            return result;
        }
    }
}
