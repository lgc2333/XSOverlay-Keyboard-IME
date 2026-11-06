using XSOverlayKeyboardIme.Ime;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class InlinePreeditWatchTests
    {
        [Fact]
        public void Observe_stays_quiet_when_the_ime_reports_composition_text()
        {
            var watch = new InlinePreeditWatch();

            Assert.False(watch.Observe(true));
            Assert.False(watch.Observe(true));
        }

        [Fact]
        public void Observe_warns_once_after_enough_misses_in_a_row()
        {
            var watch = new InlinePreeditWatch();

            Assert.False(watch.Observe(false));
            Assert.True(watch.Observe(false));
        }

        [Fact]
        public void Observe_does_not_repeat_the_warning_while_the_same_streak_continues()
        {
            var watch = new InlinePreeditWatch();

            watch.Observe(false);
            watch.Observe(false);
            Assert.False(watch.Observe(false));
        }

        [Fact]
        public void Observe_restarts_the_streak_after_a_hit()
        {
            var watch = new InlinePreeditWatch();

            watch.Observe(false);
            watch.Observe(false);
            watch.Observe(true);

            Assert.False(watch.Observe(false));
            Assert.True(watch.Observe(false));
        }
    }
}
