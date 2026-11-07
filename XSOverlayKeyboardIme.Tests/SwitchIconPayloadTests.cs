using System.Text.Json;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class SwitchIconPayloadTests
    {
        [Fact]
        public void A_missing_icon_clears_the_page_image()
        {
            Assert.Equal("null", SwitchIconPayload.Build(null));
        }

        [Fact]
        public void Icon_dimensions_and_rgba_bytes_reach_the_page_as_base64()
        {
            var icon = LanguageBarIcon.FromBgra(1, 2, new byte[] { 1, 2, 3, 255, 4, 5, 6, 128 });
            using var payload = JsonDocument.Parse(SwitchIconPayload.Build(icon));

            Assert.Equal(3, payload.RootElement.GetPropertyCount());
            Assert.Equal(1, payload.RootElement.GetProperty("width").GetInt32());
            Assert.Equal(2, payload.RootElement.GetProperty("height").GetInt32());
            Assert.Equal(
                new byte[] { 3, 2, 1, 255, 6, 5, 4, 128 },
                payload.RootElement.GetProperty("rgba").GetBytesFromBase64()
            );
        }
    }
}
