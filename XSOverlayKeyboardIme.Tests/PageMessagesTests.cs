using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class PageMessagesTests
    {
        private const string Prefix = "xso-keyboard-ime profile ";

        [Fact]
        public void A_profile_request_reads_its_index()
        {
            int index;
            Assert.True(PageMessages.TryReadProfileIndex(Prefix + "2", out index));
            Assert.Equal(2, index);
        }

        [Fact]
        public void The_first_profile_is_a_real_choice()
        {
            int index;
            Assert.True(PageMessages.TryReadProfileIndex(Prefix + "0", out index));
            Assert.Equal(0, index);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("xso-keyboard-ime profile")]
        [InlineData(Prefix)]
        [InlineData(Prefix + "-1")]
        [InlineData(Prefix + "1x")]
        [InlineData(Prefix + " 1")]
        [InlineData(Prefix + "1 ")]
        [InlineData(Prefix + "99999999999999999999")]
        [InlineData("xso-keyboard-ime switch")]
        public void Anything_but_one_whole_number_is_refused(string message)
        {
            int index;
            Assert.False(PageMessages.TryReadProfileIndex(message, out index));
            Assert.Equal(-1, index);
        }
    }
}
