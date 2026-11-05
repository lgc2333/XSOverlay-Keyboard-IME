using System.Text;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>A JavaScript string literal, so quotes and newlines survive the bridge.</summary>
    internal static class JavaScriptString
    {
        internal static string Literal(string text)
        {
            var quoted = new StringBuilder(text.Length + 2);
            quoted.Append('"');
            foreach (char character in text)
            {
                switch (character)
                {
                    case '"':
                        quoted.Append("\\\"");
                        break;
                    case '\\':
                        quoted.Append("\\\\");
                        break;
                    case '\n':
                        quoted.Append("\\n");
                        break;
                    case '\r':
                        quoted.Append("\\r");
                        break;
                    case '\t':
                        quoted.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            quoted.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            quoted.Append(character);
                        }

                        break;
                }
            }

            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
