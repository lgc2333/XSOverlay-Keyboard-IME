using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class KeyStrokeReaderTests
    {
        private static readonly string[] ReturnName = { "RETURN" };
        private static readonly string[] BackName = { "BACK" };
        private static readonly string[] UnknownName = { "F13" };
        private static readonly string[] ShiftedTab = { "LSHIFT", "TAB" };
        private static readonly string[] CharacterName = { "N" };

        private static bool Read(PageKey key, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            strokes = new List<KeyStroke>();
            return KeyStrokeReader.TryRead(key, gate, strokes);
        }

        private static bool ReadText(string text, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            return Read(new PageKey(true, null, text), gate, out strokes);
        }

        private static bool ReadNamed(string[] names, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            return Read(new PageKey(true, names, null), gate, out strokes);
        }

        [Fact]
        public void Idle_takes_a_character_and_starts_a_session()
        {
            Assert.True(ReadText("n", TakeoverGate.Idle, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.True(stroke.IsText);
            Assert.Equal('n', stroke.Text);
            Assert.False(stroke.EndsComposition);
        }

        [Fact]
        public void Idle_leaves_a_space_with_the_page()
        {
            // A space never starts a composition.
            Assert.False(ReadText(" ", TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void Idle_leaves_named_keys_with_the_page()
        {
            Assert.False(ReadNamed(ReturnName, TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void Composing_takes_a_space_and_ends_the_session()
        {
            Assert.True(ReadText(" ", TakeoverGate.Composing, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.False(stroke.IsText);
            Assert.Equal(VirtualKey.Space, stroke.Key);
            Assert.True(stroke.EndsComposition);
        }

        [Fact]
        public void Composing_takes_return_and_ends_the_session()
        {
            Assert.True(ReadNamed(ReturnName, TakeoverGate.Composing, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.Equal(VirtualKey.Return, stroke.Key);
            Assert.True(stroke.EndsComposition);
        }

        [Fact]
        public void Composing_takes_every_named_key()
        {
            Assert.True(ReadNamed(BackName, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Equal(VirtualKey.Back, Assert.Single(strokes).Key);
        }

        [Fact]
        public void Composing_carries_shift_onto_the_named_key()
        {
            Assert.True(ReadNamed(ShiftedTab, TakeoverGate.Composing, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.Equal(VirtualKey.Tab, stroke.Key);
            Assert.True(stroke.Shift);
        }

        [Fact]
        public void Composing_refuses_an_unknown_named_key()
        {
            Assert.False(ReadNamed(UnknownName, TakeoverGate.Composing, out List<KeyStroke> strokes));

            // Nothing half-read is handed on.
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_passthrough_session_takes_only_the_submit_key()
        {
            // The page keeps the characters, so no key costs a foreground grab.
            Assert.False(ReadText("n", TakeoverGate.Session, out List<KeyStroke> typed));
            Assert.Empty(typed);
            Assert.False(ReadNamed(BackName, TakeoverGate.Session, out List<KeyStroke> back));
            Assert.Empty(back);

            Assert.True(ReadText(" ", TakeoverGate.Session, out List<KeyStroke> space));
            Assert.True(Assert.Single(space).EndsComposition);
            Assert.Equal(VirtualKey.Space, space[0].Key);
        }

        [Fact]
        public void A_passthrough_session_ends_on_return_too()
        {
            Assert.True(ReadNamed(ReturnName, TakeoverGate.Session, out List<KeyStroke> strokes));
            Assert.Equal(VirtualKey.Return, Assert.Single(strokes).Key);
        }

        [Fact]
        public void A_press_that_is_not_single_is_refused()
        {
            var key = new PageKey(false, null, "n");

            Assert.False(KeyStrokeReader.TryRead(key, TakeoverGate.Composing, new List<KeyStroke>()));
        }

        [Fact]
        public void A_space_without_a_name_still_commits()
        {
            // The page reported the character but no key name for it.
            Assert.True(ReadText(" ", TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Equal(VirtualKey.Space, Assert.Single(strokes).Key);
        }

        [Fact]
        public void A_character_wins_over_a_named_key()
        {
            // The page resolved the character, so the name is not needed.
            var key = new PageKey(true, CharacterName, "n");
            var strokes = new List<KeyStroke>();

            Assert.True(KeyStrokeReader.TryRead(key, TakeoverGate.Composing, strokes));
            Assert.True(Assert.Single(strokes).IsText);
        }
    }
}
