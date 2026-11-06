using XSOverlayKeyboardIme.Ime;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class ImeModeMapTests
    {
        [Fact]
        public void The_value_the_ime_reports_as_AlphaNumeric_composes()
        {
            // Measured, not read off the enum name: AlphaNumeric is 0 and it composes.
            Assert.Equal(ImeMode.Composing, ImeModeMap.FromShim(0));
        }

        [Fact]
        public void The_value_the_ime_reports_as_Native_passes_keys_through()
        {
            Assert.Equal(ImeMode.Passthrough, ImeModeMap.FromShim(1));
        }

        [Fact]
        public void Any_other_value_is_unknown()
        {
            Assert.Equal(ImeMode.Unknown, ImeModeMap.FromShim(-1));
            Assert.Equal(ImeMode.Unknown, ImeModeMap.FromShim(7));
        }
    }
}
