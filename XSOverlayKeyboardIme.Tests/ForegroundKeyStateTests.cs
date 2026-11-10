using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests;

public sealed class ForegroundKeyStateTests
{
    [Theory]
    [InlineData(0xF0)]
    [InlineData(0xF1)]
    [InlineData(0xF2)]
    public void Persistent_nls_mode_state_does_not_block_foreground_fallback(int mode)
    {
        Assert.True(ForegroundKeyState.IsIdle(key => key == mode ? short.MinValue : (short)0));
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x02)]
    [InlineData(0x04)]
    [InlineData(0x05)]
    [InlineData(0x06)]
    [InlineData(0x41)]
    [InlineData(0x71)]
    [InlineData(0x10)]
    [InlineData(0x11)]
    [InlineData(0x12)]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    [InlineData(0xA0)]
    [InlineData(0xA1)]
    [InlineData(0xA2)]
    [InlineData(0xA3)]
    [InlineData(0xA4)]
    [InlineData(0xA5)]
    [InlineData(0xBA)]
    [InlineData(0xE2)]
    [InlineData(0xEF)]
    [InlineData(0xF3)]
    [InlineData(0xFD)]
    [InlineData(0xFE)]
    public void Other_held_keys_block_even_when_nls_modes_are_down(int held)
    {
        Assert.False(
            ForegroundKeyState.IsIdle(key => key == held || key is >= 0xF0 and <= 0xF2 ? short.MinValue : (short)0)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Released_or_previously_pressed_keys_are_idle(short state)
    {
        Assert.True(ForegroundKeyState.IsIdle(_ => state));
    }
}
