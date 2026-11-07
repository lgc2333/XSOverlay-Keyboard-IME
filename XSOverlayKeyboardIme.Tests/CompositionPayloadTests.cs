using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class CompositionPayloadTests
    {
        private static readonly string[] QuotedCandidates = { "你\"好", "a\\b" };
        private static readonly string[] LiveCandidates = { "你好" };

        [Fact]
        public void An_idle_view_carries_no_codes_and_no_candidates()
        {
            string payload = CompositionPayload.Build(CompositionView.Idle());

            Assert.Equal(
                "{\"preedit\":\"\",\"selected\":-1,\"selectionStart\":0,\"selectionEnd\":0,\"composing\":false,\"candidates\":[]}",
                payload
            );
        }

        [Fact]
        public void A_composition_without_codes_still_says_it_is_live()
        {
            // The IME may report no codes inline, so the page cannot infer this from them.
            // See ADR 0027.
            var view = new CompositionView(string.Empty, CompositionSelection.None, LiveCandidates, 0, true);

            Assert.Contains("\"composing\":true", CompositionPayload.Build(view));
        }

        [Fact]
        public void The_caret_the_ime_holds_reaches_the_page()
        {
            // The composer draws this caret, which is how a cursor key shows its effect.
            // See ADR 0031.
            var view = new CompositionView("nihc", new CompositionSelection(3, 3), LiveCandidates, 0, true);

            Assert.Contains("\"selectionStart\":3,\"selectionEnd\":3", CompositionPayload.Build(view));
        }

        [Fact]
        public void Quotes_and_newlines_survive_the_bridge()
        {
            var view = new CompositionView("a\"b\n", CompositionSelection.None, QuotedCandidates, 1, false);
            string payload = CompositionPayload.Build(view);

            Assert.Equal(
                "{\"preedit\":\"a\\\"b\\n\",\"selected\":1,\"selectionStart\":0,\"selectionEnd\":0,\"composing\":false,\"candidates\":[\"你\\\"好\",\"a\\\\b\"]}",
                payload
            );
        }
    }
}
