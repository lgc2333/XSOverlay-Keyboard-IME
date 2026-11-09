using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class NativeKeyRouteTests
    {
        [Theory]
        [InlineData(0x51, false, true, true, false)] // AltGr+Q produces @.
        [InlineData(0xDE, false, true, true, false)] // Layout dead key.
        [InlineData(0x43, false, true, false, true)] // Ctrl+C is not text.
        [InlineData(0x41, false, false, true, false)]
        [InlineData(0x08, false, false, false, true)]
        [InlineData(0x08, true, false, false, false)]
        public void Layout_text_and_shortcuts_have_distinct_recipients(
            int key,
            bool eaten,
            bool shortcut,
            bool layoutText,
            bool browser
        ) => Assert.Equal(browser, NativeKeyRoute.UsesBrowser(key, eaten, shortcut, layoutText));
    }
}
