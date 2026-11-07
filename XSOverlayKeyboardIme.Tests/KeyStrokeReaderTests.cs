using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    /// <summary>
    /// The ownership table: who answers one press of a page key. The shapes match the
    /// keyboard page, which latches modifiers and lock keys. See ADR 0036.
    /// </summary>
    public sealed class KeyStrokeReaderTests
    {
        private static readonly string[] ReturnName = { "RETURN" };
        private static readonly string[] BackName = { "BACK" };
        private static readonly string[] UnknownName = { "F13" };
        private static readonly string[] ShiftedTab = { "LSHIFT", "TAB" };
        private static readonly string[] CharacterName = { "N" };
        private static readonly string[] ControlName = { "LCONTROL" };
        private static readonly string[] ShiftName = { "LSHIFT" };
        private static readonly string[] CapsName = { "CAPITAL" };
        private static readonly string[] WinSpace = { "LWIN", "SPACE" };
        private static readonly string[] WinName = { "E05B" };
        private static readonly string[] WinR = { "LWIN", "R" };

        private static KeyOwnership Read(PageKey key, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            strokes = new List<KeyStroke>();
            return KeyStrokeReader.Read(key, gate, strokes);
        }

        private static KeyOwnership ReadText(string text, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            return Read(new PageKey(PageKeyStyle.Standard, null, text, PageModifiers.None), gate, out strokes);
        }

        private static KeyOwnership ReadNamed(string[] names, TakeoverGate gate, out List<KeyStroke> strokes)
        {
            return Read(new PageKey(PageKeyStyle.Standard, names, null, PageModifiers.None), gate, out strokes);
        }

        private static KeyOwnership ReadLatched(
            PageKeyStyle style,
            string[] names,
            TakeoverGate gate,
            out List<KeyStroke> strokes
        )
        {
            return Read(new PageKey(style, names, names[0], PageModifiers.None), gate, out strokes);
        }

        private static KeyOwnership ReadHeld(
            string text,
            PageModifiers modifiers,
            TakeoverGate gate,
            out List<KeyStroke> strokes
        )
        {
            return Read(new PageKey(PageKeyStyle.Standard, null, text, modifiers), gate, out strokes);
        }

        [Fact]
        public void Idle_takes_a_character_and_starts_a_session()
        {
            Assert.Equal(KeyOwnership.Ime, ReadText("n", TakeoverGate.Idle, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.Equal(KeyStrokeKind.Text, stroke.Kind);
            Assert.Equal('n', stroke.Text);
        }

        [Fact]
        public void Idle_leaves_a_space_with_the_page()
        {
            // A space never starts a composition.
            Assert.Equal(KeyOwnership.Page, ReadText(" ", TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void Idle_leaves_named_keys_with_the_page()
        {
            Assert.Equal(KeyOwnership.Page, ReadNamed(ReturnName, TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void Composing_takes_a_space()
        {
            Assert.Equal(KeyOwnership.Ime, ReadText(" ", TakeoverGate.Composing, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.Equal(KeyStrokeKind.Key, stroke.Kind);
            Assert.Equal(VirtualKey.Space, stroke.Key);
        }

        [Fact]
        public void Composing_takes_return()
        {
            Assert.Equal(KeyOwnership.Ime, ReadNamed(ReturnName, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Equal(VirtualKey.Return, Assert.Single(strokes).Key);
        }

        [Fact]
        public void Composing_takes_every_named_key()
        {
            Assert.Equal(KeyOwnership.Ime, ReadNamed(BackName, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Equal(VirtualKey.Back, Assert.Single(strokes).Key);
        }

        [Fact]
        public void Composing_carries_shift_onto_the_named_key()
        {
            Assert.Equal(KeyOwnership.Ime, ReadNamed(ShiftedTab, TakeoverGate.Composing, out List<KeyStroke> strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.Equal(VirtualKey.Tab, stroke.Key);
            Assert.True(stroke.Shift);
        }

        [Fact]
        public void Composing_presses_a_latched_shift_on_the_host()
        {
            // The host holds the real key down, so the IME sees a held shift. See ADR 0038.
            Assert.Equal(
                KeyOwnership.Ime,
                ReadLatched(PageKeyStyle.Sticky, ShiftName, TakeoverGate.Composing, out List<KeyStroke> strokes)
            );
            KeyStroke stroke = Assert.Single(strokes);
            Assert.Equal(KeyStrokeKind.Modifier, stroke.Kind);
            Assert.Equal(VirtualKey.LeftShift, stroke.Key);
        }

        [Fact]
        public void Composing_leaves_a_toggled_caps_with_the_page()
        {
            Assert.Equal(
                KeyOwnership.Page,
                ReadLatched(PageKeyStyle.Toggle, CapsName, TakeoverGate.Composing, out List<KeyStroke> strokes)
            );
            Assert.Empty(strokes);
        }

        [Fact]
        public void Idle_presses_a_latched_shift_on_the_host()
        {
            Assert.Equal(
                KeyOwnership.Ime,
                ReadLatched(PageKeyStyle.Sticky, ShiftName, TakeoverGate.Idle, out List<KeyStroke> strokes)
            );
            Assert.Equal(KeyStrokeKind.Modifier, Assert.Single(strokes).Kind);
        }

        [Fact]
        public void Idle_leaves_a_toggled_caps_with_the_page()
        {
            Assert.Equal(
                KeyOwnership.Page,
                ReadLatched(PageKeyStyle.Toggle, CapsName, TakeoverGate.Idle, out List<KeyStroke> strokes)
            );
            Assert.Empty(strokes);
        }

        [Fact]
        public void Composing_presses_a_latched_control_on_the_host()
        {
            Assert.Equal(
                KeyOwnership.Ime,
                ReadLatched(PageKeyStyle.Sticky, ControlName, TakeoverGate.Composing, out List<KeyStroke> strokes)
            );
            Assert.Equal(VirtualKey.LeftControl, Assert.Single(strokes).Key);
        }

        [Fact]
        public void Composing_swallows_an_unknown_named_key()
        {
            // The page must not edit the composer while a composition holds it. See ADR 0026.
            Assert.Equal(
                KeyOwnership.Swallow,
                ReadNamed(UnknownName, TakeoverGate.Composing, out List<KeyStroke> strokes)
            );
            Assert.Empty(strokes);
        }

        [Fact]
        public void Idle_leaves_an_unknown_named_key_with_the_page()
        {
            Assert.Equal(KeyOwnership.Page, ReadNamed(UnknownName, TakeoverGate.Idle, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_foreign_modifier_stays_with_the_page()
        {
            // Only Shift is read, because the page already folded it into the character.
            // Every other combination belongs to the page. See ADR 0026.
            var key = new PageKey(PageKeyStyle.Standard, ControlName, "a", PageModifiers.None);

            Assert.Equal(KeyOwnership.Page, KeyStrokeReader.Read(key, TakeoverGate.Composing, new List<KeyStroke>()));
        }

        [Fact]
        public void A_shifted_character_still_starts_a_session()
        {
            var key = new PageKey(PageKeyStyle.Standard, ShiftName, "N", PageModifiers.None);

            Assert.Equal(KeyOwnership.Ime, KeyStrokeReader.Read(key, TakeoverGate.Idle, new List<KeyStroke>()));
        }

        [Fact]
        public void A_press_the_plugin_does_not_model_is_refused()
        {
            var key = new PageKey(PageKeyStyle.Other, null, "n", PageModifiers.None);

            Assert.Equal(KeyOwnership.Page, KeyStrokeReader.Read(key, TakeoverGate.Composing, new List<KeyStroke>()));
        }

        [Fact]
        public void A_space_without_a_name_still_commits()
        {
            // The page reported the character but no key name for it.
            Assert.Equal(KeyOwnership.Ime, ReadText(" ", TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Equal(VirtualKey.Space, Assert.Single(strokes).Key);
        }

        [Fact]
        public void A_character_wins_over_a_named_key()
        {
            // The page resolved the character, so the name is not needed.
            var key = new PageKey(PageKeyStyle.Standard, CharacterName, "n", PageModifiers.None);

            Assert.Equal(KeyOwnership.Ime, KeyStrokeReader.Read(key, TakeoverGate.Composing, new List<KeyStroke>()));
        }

        [Fact]
        public void A_held_control_stays_with_the_page()
        {
            // A combination belongs to the page. See ADR 0029.
            Assert.Equal(
                KeyOwnership.Page,
                ReadHeld("a", PageModifiers.Control, TakeoverGate.Composing, out List<KeyStroke> strokes)
            );
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_held_alt_stays_with_the_page()
        {
            Assert.Equal(
                KeyOwnership.Page,
                ReadHeld("a", PageModifiers.Alt, TakeoverGate.Composing, out List<KeyStroke> strokes)
            );
            Assert.Empty(strokes);
        }

        [Fact]
        public void Win_space_belongs_to_the_system()
        {
            // The switch reaches our own IME, so the key must not be swallowed. See ADR 0036.
            Assert.Equal(
                KeyOwnership.System,
                ReadHeld(" ", PageModifiers.Meta, TakeoverGate.Idle, out List<KeyStroke> strokes)
            );
            Assert.Equal(KeyStrokeKind.System, Assert.Single(strokes).Kind);
        }

        [Fact]
        public void Win_space_named_only_by_the_page_belongs_to_the_system()
        {
            var key = new PageKey(PageKeyStyle.Standard, WinSpace, " ", PageModifiers.None);

            Assert.Equal(KeyOwnership.System, KeyStrokeReader.Read(key, TakeoverGate.Composing, new List<KeyStroke>()));
        }

        [Fact]
        public void Every_other_win_combination_is_swallowed()
        {
            // Windows answers Win combinations itself, so the character must not reach
            // the page. See ADR 0029.
            Assert.Equal(KeyOwnership.Swallow, ReadNamed(WinR, TakeoverGate.Composing, out List<KeyStroke> strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void A_latched_win_is_pressed_on_the_host()
        {
            // Win is a modifier like the others, so it is held for real. See ADR 0038.
            Assert.Equal(
                KeyOwnership.Ime,
                ReadLatched(PageKeyStyle.Sticky, WinName, TakeoverGate.Idle, out List<KeyStroke> strokes)
            );
            Assert.Equal(VirtualKey.LeftWin, Assert.Single(strokes).Key);
        }

        [Fact]
        public void A_held_shift_still_starts_a_session()
        {
            Assert.Equal(
                KeyOwnership.Ime,
                ReadHeld("N", PageModifiers.Shift, TakeoverGate.Idle, out List<KeyStroke> strokes)
            );
            Assert.Equal(KeyStrokeKind.Text, Assert.Single(strokes).Kind);
        }
    }
}
