using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class ComposerStatePayloadTests
    {
        [Fact]
        public void A_composer_without_focus_asks_the_page_to_show_nothing()
        {
            Assert.Equal("{\"active\":false,\"ime\":\"\"}", ComposerStatePayload.Build(ComposerState.Hidden));
        }

        [Fact]
        public void A_focused_composer_names_the_ime_it_composes_with()
        {
            var state = new ComposerState(true, "小狼毫");

            Assert.Equal("{\"active\":true,\"ime\":\"小狼毫\"}", ComposerStatePayload.Build(state));
        }

        [Fact]
        public void Quotes_in_an_ime_name_survive_the_bridge()
        {
            var state = new ComposerState(true, "a\"b\\c");

            Assert.Equal("{\"active\":true,\"ime\":\"a\\\"b\\\\c\"}", ComposerStatePayload.Build(state));
        }
    }
}
