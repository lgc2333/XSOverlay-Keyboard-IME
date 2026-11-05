using XSOverlayKeyboardIme.Page;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class BarActionTests
    {
        [Fact]
        public void A_ready_line_is_recognised()
        {
            Assert.True(BarAction.TryParse("xso-keyboard-ime ready", out BarAction action));
            Assert.Equal(BarActionKind.Ready, action.Kind);
        }

        [Theory]
        [InlineData("xso-keyboard-ime pick 0", 0)]
        [InlineData("xso-keyboard-ime pick 9", 9)]
        public void A_pick_carries_the_candidate_index(string message, int expected)
        {
            Assert.True(BarAction.TryParse(message, out BarAction action));
            Assert.Equal(BarActionKind.Pick, action.Kind);
            Assert.Equal(expected, action.Value);
        }

        [Theory]
        [InlineData("xso-keyboard-ime turn -1", -1)]
        [InlineData("xso-keyboard-ime turn 1", 1)]
        public void A_turn_carries_its_direction(string message, int expected)
        {
            Assert.True(BarAction.TryParse(message, out BarAction action));
            Assert.Equal(BarActionKind.Turn, action.Kind);
            Assert.Equal(expected, action.Value);
        }

        [Fact]
        public void The_prefix_is_matched_without_case()
        {
            Assert.True(BarAction.TryParse("XSO-Keyboard-IME PICK 1", out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("hello")]
        [InlineData("xso-keyboard-ime")]
        [InlineData("xso-keyboard-ime pick")]
        [InlineData("xso-keyboard-ime pick -1")]
        [InlineData("xso-keyboard-ime pick 1 2")]
        [InlineData("xso-keyboard-ime turn 0")]
        [InlineData("xso-keyboard-ime page 1")]
        [InlineData("xso-keyboard-ime stop")]
        public void Anything_else_is_ignored(string message)
        {
            Assert.False(BarAction.TryParse(message, out _));
        }
    }
}
