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
            using var payload = JsonDocument.Parse(SwitchIconPayload.Build(new InputModeView(icon)));

            Assert.Equal("image", payload.RootElement.GetProperty("kind").GetString());
            Assert.Equal(1, payload.RootElement.GetProperty("width").GetInt32());
            Assert.Equal(2, payload.RootElement.GetProperty("height").GetInt32());
            Assert.Equal(
                new byte[] { 3, 2, 1, 255, 6, 5, 4, 128 },
                payload.RootElement.GetProperty("rgba").GetBytesFromBase64()
            );
        }

        [Fact]
        public void A_glyph_keeps_its_system_font_and_disabled_state()
        {
            using var payload = JsonDocument.Parse(
                SwitchIconPayload.Build(
                    new InputModeView(glyph: "\uE982", font: "Segoe Fluent Icons", enabled: false, tooltip: "中文模式")
                )
            );
            Assert.Equal("glyph", payload.RootElement.GetProperty("kind").GetString());
            Assert.Equal("\uE982", payload.RootElement.GetProperty("glyph").GetString());
            Assert.Equal("Segoe Fluent Icons", payload.RootElement.GetProperty("font").GetString());
            Assert.False(payload.RootElement.GetProperty("enabled").GetBoolean());
            Assert.Equal("中文模式", payload.RootElement.GetProperty("tooltip").GetString());
        }

        [Fact]
        public void An_icon_with_no_alpha_keeps_its_transparent_mask_on_the_page()
        {
            var icon = LanguageBarIcon.FromBgra(
                2,
                1,
                new byte[] { 1, 2, 3, 0, 4, 5, 6, 0 },
                new byte[] { 255, 255, 255, 0, 0, 0, 0, 0 }
            );
            using var payload = JsonDocument.Parse(SwitchIconPayload.Build(new InputModeView(icon)));
            Assert.Equal(
                new byte[] { 3, 2, 1, 0, 6, 5, 4, 255 },
                payload.RootElement.GetProperty("rgba").GetBytesFromBase64()
            );
        }

        [Fact]
        public void A_monochrome_mask_uses_the_page_foreground_color()
        {
            var icon = LanguageBarIcon.FromMonochrome(
                2,
                1,
                new byte[] { 255, 255, 255, 0, 255, 255, 255, 0, 0, 0, 0, 0, 255, 255, 255, 0 }
            );
            using var payload = JsonDocument.Parse(SwitchIconPayload.Build(new InputModeView(icon)));
            Assert.True(payload.RootElement.GetProperty("tint").GetBoolean());
            Assert.Equal(
                new byte[] { 255, 255, 255, 0, 255, 255, 255, 255 },
                payload.RootElement.GetProperty("rgba").GetBytesFromBase64()
            );
        }

        [Fact]
        public void A_half_transparent_red_icon_reaches_canvas_without_dark_edges()
        {
            var icon = LanguageBarIcon.FromBgra(1, 1, new byte[] { 0, 0, 128, 128 }, premultiplied: true);
            using var payload = JsonDocument.Parse(SwitchIconPayload.Build(new InputModeView(icon)));
            Assert.Equal(new byte[] { 255, 0, 0, 128 }, payload.RootElement.GetProperty("rgba").GetBytesFromBase64());
        }
    }
}
