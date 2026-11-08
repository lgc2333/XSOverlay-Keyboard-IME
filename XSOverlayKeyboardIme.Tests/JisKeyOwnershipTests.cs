using System.Collections.Generic;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class JisKeyOwnershipTests
    {
        [Theory]
        [InlineData((int)PageKeyStyle.Sticky, (int)PageModifiers.None, (int)KeyOwnership.Page)]
        [InlineData((int)PageKeyStyle.Toggle, (int)PageModifiers.None, (int)KeyOwnership.Page)]
        [InlineData((int)PageKeyStyle.Other, (int)PageModifiers.None, (int)KeyOwnership.Page)]
        [InlineData((int)PageKeyStyle.Standard, (int)PageModifiers.Control, (int)KeyOwnership.Page)]
        [InlineData((int)PageKeyStyle.Standard, (int)PageModifiers.Alt, (int)KeyOwnership.Page)]
        [InlineData((int)PageKeyStyle.Standard, (int)PageModifiers.Meta, (int)KeyOwnership.Swallow)]
        public void Ime_keys_keep_the_existing_style_and_combination_rules(int style, int modifiers, int ownership)
        {
            foreach (TakeoverGate gate in new[] { TakeoverGate.Idle, TakeoverGate.Composing })
            foreach (int code in new[] { 0x15, 0x1C, 0x1D })
            {
                var key = new PageKey((PageKeyStyle)style, null, "f", (PageModifiers)modifiers, (VirtualKey)code);
                var strokes = new List<KeyStroke>();
                Assert.Equal((KeyOwnership)ownership, KeyStrokeReader.Read(key, gate, strokes));
                Assert.Empty(strokes);
            }
        }

        [Theory]
        [InlineData(0x15, "KANA")]
        [InlineData(0x15, "HANGUL")]
        [InlineData(0x15, "HANGEUL")]
        [InlineData(0x15, "21")]
        [InlineData(0x1C, "CONVERT")]
        [InlineData(0x1C, "28")]
        [InlineData(0x1D, "NONCONVERT")]
        [InlineData(0x1D, "29")]
        public void A_standard_ime_key_keeps_its_native_code_regardless_of_label(int code, string name)
        {
            foreach (TakeoverGate gate in new[] { TakeoverGate.Idle, TakeoverGate.Composing })
            foreach (string label in new[] { "", "変換", "平/片假", "f" })
            foreach (int shiftSource in new[] { 0, 1, 2 })
            {
                string[] names = shiftSource == 2 ? new[] { "LSHIFT", name } : new[] { name };
                PageModifiers modifiers = shiftSource == 1 ? PageModifiers.Shift : PageModifiers.None;
                var key = new PageKey(PageKeyStyle.Standard, names, label, modifiers, (VirtualKey)code);
                var strokes = new List<KeyStroke>();

                Assert.Equal(KeyOwnership.Ime, KeyStrokeReader.Read(key, gate, strokes));
                KeyStroke stroke = Assert.Single(strokes);
                Assert.Equal(KeyStrokeKind.Key, stroke.Kind);
                Assert.Equal((VirtualKey)code, stroke.Key);
                Assert.Equal(shiftSource != 0, stroke.Shift);
            }
        }
    }
}
