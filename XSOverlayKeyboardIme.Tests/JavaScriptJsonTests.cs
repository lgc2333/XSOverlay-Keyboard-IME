using System.Text.Json;
using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class JavaScriptJsonTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("a\"b\\c\r\n\t\b\f\0\u001f")]
        [InlineData("中文😀\u2028\u2029")]
        [InlineData("\");throw new Error('escaped');(\"")]
        public void Text_is_ascii_javascript_source_and_round_trips(string text)
        {
            string json = JavaScriptJson.Serialize(text);

            Assert.All(json, character => Assert.InRange((int)character, 0x20, 0x7e));
            Assert.Equal(text, JsonSerializer.Deserialize<string>(json));
        }

        [Fact]
        public void Unpaired_utf16_surrogates_are_escaped_before_the_script_crosses_the_bridge()
        {
            string text = "\ud800a\udfff";

            Assert.Equal("\"\\ud800a\\udfff\"", JavaScriptJson.Serialize(text));
        }
    }
}
