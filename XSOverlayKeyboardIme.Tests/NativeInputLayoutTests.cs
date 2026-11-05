using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Interop;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class NativeInputLayoutTests
    {
        [Fact]
        public void SendInput_payload_matches_the_x64_layout()
        {
            Assert.Equal(40, Marshal.SizeOf<NativeInput>());
            Assert.Equal(8, Marshal.OffsetOf<NativeInput>(nameof(NativeInput.Data)).ToInt32());
        }

        [Fact]
        public void The_input_union_is_sized_by_the_mouse_arm()
        {
            Assert.Equal(32, Marshal.SizeOf<InputUnion>());
        }
    }
}
