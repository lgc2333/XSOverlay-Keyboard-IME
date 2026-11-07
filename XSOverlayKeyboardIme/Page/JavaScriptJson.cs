using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>Serializes values for JavaScript arguments, escaping every non-ASCII UTF-16 code unit.</summary>
    internal static class JavaScriptJson
    {
        internal static string Serialize(object value)
        {
            var serializer = new JsonSerializer { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            serializer.Serialize(writer, value);
            return writer.ToString();
        }
    }
}
