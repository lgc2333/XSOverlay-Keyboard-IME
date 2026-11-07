using System;
using XSOverlayKeyboardIme.Ime;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class LanguageProfileTests
    {
        private static readonly Guid Weasel = new Guid("A3F4CDED-B1E9-41EE-9CA6-7B4D0DE6CB0A");
        private static readonly Guid Pinyin = new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E");

        [Fact]
        public void The_profile_this_thread_has_active_is_the_one_picked()
        {
            // Two IMEs of one language share a keyboard layout, so only this flag tells
            // which one composes. See ADR 0035.
            var listed = new[]
            {
                new LanguageProfile(Weasel, 0x0804, Guid.NewGuid(), false),
                new LanguageProfile(Pinyin, 0x0804, Guid.NewGuid(), true),
            };

            Assert.True(LanguageProfile.TryFindActive(listed, out LanguageProfile active));
            Assert.Equal(Pinyin, active.Service);
        }

        [Fact]
        public void No_active_row_is_reported_rather_than_guessed()
        {
            var listed = new[] { new LanguageProfile(Weasel, 0x0804, Guid.NewGuid(), false) };

            Assert.False(LanguageProfile.TryFindActive(listed, out _));
        }

        [Fact]
        public void An_empty_page_has_no_active_profile()
        {
            Assert.False(LanguageProfile.TryFindActive(Array.Empty<LanguageProfile>(), out _));
        }
    }
}
