using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class NativeKeyNameTests
    {
        [Theory]
        [InlineData(0x08, "Backspace")]
        [InlineData(0x11, "Control")]
        [InlineData(0x25, "ArrowLeft")]
        [InlineData(0x41, "A")]
        [InlineData(0x70, "F1")]
        [InlineData(0xDE, "'")]
        [InlineData(0xE2, "\\")]
        public void Names_native_browser_edges_explicitly(int key, string expected)
        {
            Assert.True(NativeKeyName.TryRead(key, out string name));
            Assert.Equal(expected, name);
        }

        [Theory]
        [InlineData(0x20)]
        [InlineData(0x41)]
        [InlineData(0xBA)]
        public void Ordinary_characters_use_host_results(int key) => Assert.True(NativeKeyName.IsCharacter(key));
    }
}
