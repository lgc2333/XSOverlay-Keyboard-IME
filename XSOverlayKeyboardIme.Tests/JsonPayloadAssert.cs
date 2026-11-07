using System.Text.Json;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    internal static class JsonPayloadAssert
    {
        internal static void Equal(string expected, string actual)
        {
            using var expectedJson = JsonDocument.Parse(expected);
            using var actualJson = JsonDocument.Parse(actual);
            Assert.True(JsonElement.DeepEquals(expectedJson.RootElement, actualJson.RootElement), actual);
        }
    }
}
