using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class CompositionPayloadTests
    {
        private static readonly string[] QuotedCandidates = { "你\"好", "a\\b" };

        [Fact]
        public void An_idle_view_keeps_the_ime_name_and_an_empty_page()
        {
            string payload = CompositionPayload.Build(CompositionView.Idle("小狼毫"));

            Assert.Equal("{\"preedit\":\"\",\"selected\":-1,\"ime\":\"小狼毫\",\"candidates\":[]}", payload);
        }

        [Fact]
        public void Quotes_and_newlines_survive_the_bridge()
        {
            var view = new CompositionView("a\"b\n", QuotedCandidates, 1, "x");
            string payload = CompositionPayload.Build(view);

            Assert.Equal(
                "{\"preedit\":\"a\\\"b\\n\",\"selected\":1,\"ime\":\"x\",\"candidates\":[\"你\\\"好\",\"a\\\\b\"]}",
                payload
            );
        }
    }
}
