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
        private static readonly string[] ControlName = { "LCTRL" };
        private static readonly string[] ShiftName = { "LSHIFT" };
        private static readonly string[] WinSpace = { "LWIN", "SPACE" };
        private static readonly string[] WinName = { "LWIN" };

        private static bool Read(PageKey key, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            strokes = new List<KeyStroke>();
            return KeyStrokeReader.TryRead(key, gate, strokes);
        }

        private static bool ReadText(string text, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            return Read(new PageKey(true, null, text, PageModifiers.None), gate, out strokes);
        }

        private static bool ReadNamed(string[] names, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            return Read(new PageKey(true, names, null, PageModifiers.None), gate, out strokes);
        }

        private static bool ReadHeld(
            string text,
            PageModifiers modifiers,
            TakeoverGate gate,
            out List<KeyStroke> strokes
        )
        {
            return Read(new PageKey(true, null, text, modifiers), gate, out strokes);
        }

        [Fact]
        public void Idle_takes_a_character_and_starts_a_session()
        {
            Assert.True(ReadText("n", TakeoverGate.Idle, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.True(stroke.IsText);
            Assert.Equal('n', stroke.Text);
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
        public void Composing_takes_a_space()
        {
            Assert.True(ReadText(" ", TakeoverGate.Composing, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.False(stroke.IsText);
            Assert.Equal(VirtualKey.Space, stroke.Key);
        }

        [Fact]
        public void Composing_takes_return()
        {
            Assert.True(ReadNamed(ReturnName, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Equal(VirtualKey.Return, Assert.Single(strokes).Key);
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
        public void Composing_swallows_an_unknown_named_key()
        {
            // The page must not edit the composer while a composition holds it. See ADR 0026.
            Assert.True(ReadNamed(UnknownName, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void Idle_leaves_an_unknown_named_key_with_the_page()
        {
            Assert.False(ReadNamed(UnknownName, TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_foreign_modifier_stays_with_the_page()
        {
            // Only Shift is read, because the page already folded it into the character.
            // Every other combination belongs to the page. See ADR 0026.
            var key = new PageKey(true, ControlName, "a", PageModifiers.None);
            var strokes = new List<KeyStroke>();

            Assert.False(KeyStrokeReader.TryRead(key, TakeoverGate.Composing, strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_shifted_character_still_starts_a_session()
        {
            var key = new PageKey(true, ShiftName, "N", PageModifiers.None);

            Assert.True(KeyStrokeReader.TryRead(key, TakeoverGate.Idle, new List<KeyStroke>()));
        }

        [Fact]
        public void A_press_that_is_not_single_is_refused()
        {
            var key = new PageKey(false, null, "n", PageModifiers.None);

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
            var key = new PageKey(true, CharacterName, "n", PageModifiers.None);
            var strokes = new List<KeyStroke>();

            Assert.True(KeyStrokeReader.TryRead(key, TakeoverGate.Composing, strokes));
            Assert.True(Assert.Single(strokes).IsText);
        }

        [Fact]
        public void A_held_control_stays_with_the_page()
        {
            // A combination belongs to the page. See ADR 0029.
            Assert.False(ReadHeld("a", PageModifiers.Control, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_held_alt_stays_with_the_page()
        {
            Assert.False(ReadHeld("a", PageModifiers.Alt, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_held_win_is_taken_and_fed_nothing()
        {
            // Windows answers Win combinations itself, so the character must not reach
            // the page. See ADR 0029.
            Assert.True(ReadHeld(" ", PageModifiers.Meta, TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_win_named_only_by_the_page_is_taken_and_fed_nothing()
        {
            var key = new PageKey(true, WinSpace, " ", PageModifiers.None);
            var strokes = new List<KeyStroke>();

            Assert.True(KeyStrokeReader.TryRead(key, TakeoverGate.Idle, strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_press_that_carries_only_a_modifier_stays_with_the_page()
        {
            // The game records the modifier there, so the combination that follows is
            // recognized. See ADR 0029.
            var key = new PageKey(true, WinName, string.Empty, PageModifiers.Meta);
            var strokes = new List<KeyStroke>();

            Assert.False(KeyStrokeReader.TryRead(key, TakeoverGate.Composing, strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_held_shift_still_starts_a_session()
        {
            Assert.True(ReadHeld("N", PageModifiers.Shift, TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.True(Assert.Single(strokes).IsText);
        }
    }
}
