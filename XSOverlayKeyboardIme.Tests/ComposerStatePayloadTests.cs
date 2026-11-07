using System.Globalization;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class ComposerStatePayloadTests
    {
        private static readonly ImeChoice[] TwoImes =
        {
            new ImeChoice("小狼毫", "中文(简体)", default(LanguageProfile)),
            new ImeChoice("微软拼音", "中文(简体)", default(LanguageProfile)),
        };

        [Fact]
        public void A_composer_without_focus_asks_the_page_to_show_nothing()
        {
            JsonPayloadAssert.Equal(
                "{\"active\":false,\"ime\":\"\",\"activeProfile\":-1,\"profiles\":[]}",
                ComposerStatePayload.Build(ComposerState.Hidden)
            );
        }

        [Fact]
        public void A_focused_composer_names_the_ime_it_composes_with()
        {
            var state = new ComposerState(true, "小狼毫");

            JsonPayloadAssert.Equal(
                "{\"active\":true,\"ime\":\"小狼毫\",\"activeProfile\":-1,\"profiles\":[]}",
                ComposerStatePayload.Build(state)
            );
        }

        [Fact]
        public void Quotes_in_an_ime_name_survive_the_bridge()
        {
            var state = new ComposerState(true, "a\"b\\c");

            JsonPayloadAssert.Equal(
                "{\"active\":true,\"ime\":\"a\\\"b\\\\c\",\"activeProfile\":-1,\"profiles\":[]}",
                ComposerStatePayload.Build(state)
            );
        }

        [Fact]
        public void The_dropdown_lists_every_ime_in_the_order_the_page_may_ask_for()
        {
            var state = new ComposerState(true, "小狼毫", TwoImes, 0);

            JsonPayloadAssert.Equal(
                "{\"active\":true,\"ime\":\"小狼毫\",\"activeProfile\":0,\"profiles\":[{\"name\":\"小狼毫\",\"language\":\"中文(简体)\"},{\"name\":\"微软拼音\",\"language\":\"中文(简体)\"}]}",
                ComposerStatePayload.Build(state)
            );
        }

        [Fact]
        public void Profile_indices_use_json_numbers_under_any_culture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "~";
            try
            {
                CultureInfo.CurrentCulture = culture;
                JsonPayloadAssert.Equal(
                    "{\"active\":false,\"ime\":\"\",\"activeProfile\":-1,\"profiles\":[]}",
                    ComposerStatePayload.Build(ComposerState.Hidden)
                );
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
