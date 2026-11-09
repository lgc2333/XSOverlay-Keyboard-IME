using XSOverlayKeyboardIme.Input;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public class HostTextBufferTests
    {
        [Fact]
        public void SeparateMessagesDeliverOneCompleteSupplementaryCharacter()
        {
            var buffer = new HostTextBuffer();
            buffer.Append('\ud83d');
            Assert.Empty(buffer.Take());
            buffer.Append('\ude00');
            Assert.Equal("😀", buffer.Take());
            Assert.Empty(buffer.Take());
        }

        [Fact]
        public void InvalidSurrogatesCannotCorruptTheNextOrdinaryResult()
        {
            var buffer = new HostTextBuffer();
            buffer.Append('\ude00');
            buffer.Append('a');
            buffer.Append('\ud83d');
            Assert.Equal("a", buffer.Take());
            buffer.Append('中');
            Assert.Equal("中", buffer.Take());
        }
    }
}
