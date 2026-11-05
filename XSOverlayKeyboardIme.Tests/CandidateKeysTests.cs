using System.Collections.Generic;
using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class CandidateKeysTests
    {
        [Fact]
        public void An_ime_with_candidate_digits_takes_the_digit_of_the_index()
        {
            var strokes = new List<KeyStroke>();

            Assert.True(CandidateKeys.ForPick(2, digitPicks: true, strokes));

            KeyStroke stroke = Assert.Single(strokes);
            Assert.True(stroke.IsText);
            Assert.Equal('3', stroke.Text);
        }

        [Fact]
        public void The_first_candidate_is_digit_one()
        {
            var strokes = new List<KeyStroke>();

            CandidateKeys.ForPick(0, digitPicks: true, strokes);

            Assert.Equal('1', strokes[0].Text);
        }

        [Fact]
        public void The_tenth_candidate_falls_back_to_the_arrows()
        {
            var strokes = new List<KeyStroke>();

            Assert.True(CandidateKeys.ForPick(9, digitPicks: true, strokes));

            Assert.Equal(10, strokes.Count);
            Assert.Equal(VirtualKey.Down, strokes[8].Key);
            Assert.Equal(VirtualKey.Return, strokes[9].Key);
        }

        [Fact]
        public void An_ime_without_candidate_digits_walks_down_and_confirms()
        {
            var strokes = new List<KeyStroke>();

            Assert.True(CandidateKeys.ForPick(2, digitPicks: false, strokes));

            Assert.Equal(3, strokes.Count);
            Assert.Equal(VirtualKey.Down, strokes[0].Key);
            Assert.Equal(VirtualKey.Down, strokes[1].Key);
            Assert.Equal(VirtualKey.Return, strokes[2].Key);
        }

        [Fact]
        public void The_first_candidate_only_confirms()
        {
            var strokes = new List<KeyStroke>();

            CandidateKeys.ForPick(0, digitPicks: false, strokes);

            KeyStroke stroke = Assert.Single(strokes);
            Assert.Equal(VirtualKey.Return, stroke.Key);
        }

        [Fact]
        public void A_negative_index_has_no_keys()
        {
            var strokes = new List<KeyStroke>();

            Assert.False(CandidateKeys.ForPick(-1, digitPicks: false, strokes));
            Assert.Empty(strokes);
        }

        [Fact]
        public void Turning_backwards_is_page_up()
        {
            var strokes = new List<KeyStroke>();

            CandidateKeys.ForTurn(-1, strokes);

            Assert.Equal(VirtualKey.PageUp, Assert.Single(strokes).Key);
        }

        [Fact]
        public void Turning_forwards_is_page_down()
        {
            var strokes = new List<KeyStroke>();

            CandidateKeys.ForTurn(1, strokes);

            Assert.Equal(VirtualKey.PageDown, Assert.Single(strokes).Key);
        }
    }
}
