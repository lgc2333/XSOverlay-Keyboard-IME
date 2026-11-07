using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class KeyStrokeTests
    {
        [Theory]
        [InlineData((int)KeyStrokeKind.ModifierDown)]
        [InlineData((int)KeyStrokeKind.ModifierUp)]
        [InlineData((int)KeyStrokeKind.ModifierPress)]
        public void Modifier_transitions_do_not_count_as_composition(int kind)
        {
            var transition = (KeyStrokeKind)kind;
            KeyStroke stroke = KeyStroke.FromModifier(VirtualKey.LeftShift, transition);

            Assert.Equal(transition, stroke.Kind);
            Assert.Equal(VirtualKey.LeftShift, stroke.Key);
            Assert.True(stroke.IsModifier);
            Assert.False(stroke.CountsAsComposition);
        }
    }
}
