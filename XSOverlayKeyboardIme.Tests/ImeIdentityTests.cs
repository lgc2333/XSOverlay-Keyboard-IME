using XSOverlayKeyboardIme.Composition;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class ImeIdentityTests
    {
        [Theory]
        [InlineData("小狼毫")]
        [InlineData("Weasel")]
        [InlineData("Rime input method")]
        public void The_rime_family_reads_candidate_digits(string name)
        {
            Assert.True(new ImeIdentity(name).ReadsCandidateDigits);
        }

        [Theory]
        [InlineData("Microsoft IME")]
        [InlineData("微软日语 IME")]
        [InlineData("")]
        public void Another_ime_takes_arrows(string name)
        {
            Assert.False(new ImeIdentity(name).ReadsCandidateDigits);
        }

        [Fact]
        public void An_unknown_ime_has_no_name()
        {
            Assert.Equal(string.Empty, ImeIdentity.Unknown.Name);
            Assert.False(ImeIdentity.Unknown.ReadsCandidateDigits);
        }
    }
}
