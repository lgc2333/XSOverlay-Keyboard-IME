using System.Collections.Generic;
using System.Linq;
using XSOverlayKeyboardIme.Chatbox;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class ChatboxBroadcastTests
    {
        [Fact]
        public void Updates_at_most_once_per_second_and_sends_the_latest_pending_text()
        {
            var messages = new List<(string Address, object[] Arguments)>();
            var broadcast = new ChatboxBroadcast((address, arguments) => messages.Add((address, arguments)));
            broadcast.Update(true, "first", false);
            broadcast.Pump(0);
            broadcast.Update(true, "intermediate", false);
            broadcast.Pump(200);
            broadcast.Update(true, "latest", false);
            broadcast.Pump(999);
            Assert.Equal("first", Assert.Single(Texts(messages)));
            broadcast.Pump(1000);
            Assert.Collection(
                Texts(messages),
                text => Assert.Equal("first", text),
                text => Assert.Equal("latest", text)
            );
            Assert.All(
                messages.Where(message => message.Address == "/chatbox/input"),
                message =>
                {
                    Assert.Equal(true, message.Arguments[1]);
                    Assert.Equal(false, message.Arguments[2]);
                }
            );
        }

        [Fact]
        public void Limits_the_preview_without_splitting_a_surrogate_pair_or_resending_overflow_edits()
        {
            var messages = new List<(string Address, object[] Arguments)>();
            var broadcast = new ChatboxBroadcast((address, arguments) => messages.Add((address, arguments)));
            string prefix = new string('a', 143);
            broadcast.Update(true, prefix + "😀tail", false);
            broadcast.Pump(0);
            Assert.Equal(prefix, Assert.Single(Texts(messages)));
            broadcast.Update(true, prefix + "😀changed tail", false);
            broadcast.Pump(1000);
            Assert.Single(Texts(messages));
            broadcast.Update(true, "b" + new string('a', 142) + "😀tail", false);
            broadcast.Pump(1000);
            Assert.Equal("b" + new string('a', 142), Texts(messages)[1]);
        }

        [Fact]
        public void Submission_cancels_pending_previews_and_preserves_the_native_message()
        {
            var messages = new List<(string Address, object[] Arguments)>();
            var broadcast = new ChatboxBroadcast((address, arguments) => messages.Add((address, arguments)));
            broadcast.Update(true, "preview", false);
            broadcast.Pump(0);
            broadcast.Update(true, "queued preview", false);
            broadcast.BeginSubmission();
            broadcast.Update(true, "stale queued page update", false);
            broadcast.Pump(1000);
            broadcast.EndSubmission(1200, true);
            broadcast.Update(true, "late pre-submit page update", false);
            broadcast.Pump(2000);
            Assert.Equal("preview", Assert.Single(Texts(messages)));
            Assert.Contains(
                messages,
                message => message.Address == "/chatbox/typing" && Equals(message.Arguments[0], false)
            );
            broadcast.Update(true, "next draft", false, broadcast.Revision);
            broadcast.Pump(2199);
            Assert.Single(Texts(messages));
            broadcast.Pump(2200);
            Assert.Equal("next draft", Texts(messages)[1]);
        }

        [Fact]
        public void Failed_submission_restores_the_previous_draft_without_a_new_page_snapshot()
        {
            var messages = new List<(string Address, object[] Arguments)>();
            var broadcast = new ChatboxBroadcast((address, arguments) => messages.Add((address, arguments)));
            broadcast.Update(true, "draft", false);
            broadcast.Pump(0);
            broadcast.BeginSubmission();
            broadcast.Pump(100);
            Assert.False((bool)messages[^1].Arguments[0]);

            broadcast.EndSubmission(200, false);
            broadcast.Pump(200);
            Assert.True((bool)messages[^1].Arguments[0]);
            broadcast.Pump(1200);
            Assert.Collection(
                Texts(messages),
                text => Assert.Equal("draft", text),
                text => Assert.Equal("draft", text)
            );
        }

        [Fact]
        public void Closing_during_a_failed_submission_does_not_restore_the_old_draft()
        {
            var messages = new List<(string Address, object[] Arguments)>();
            var broadcast = new ChatboxBroadcast((address, arguments) => messages.Add((address, arguments)));
            broadcast.Update(true, "draft", false);
            broadcast.Pump(0);
            broadcast.BeginSubmission();
            broadcast.EndEditing();
            broadcast.Pump(100);
            broadcast.EndSubmission(200, false);
            broadcast.Pump(1200);
            Assert.Equal("draft", Assert.Single(Texts(messages)));
            Assert.False((bool)messages[^1].Arguments[0]);
        }

        [Fact]
        public void Clearing_or_disabling_drops_pending_text_and_reactivation_sends_whitespace_unchanged()
        {
            var messages = new List<(string Address, object[] Arguments)>();
            var broadcast = new ChatboxBroadcast((address, arguments) => messages.Add((address, arguments)));
            broadcast.Update(true, " ", false);
            broadcast.Pump(0);
            broadcast.Update(true, "queued", false);
            broadcast.Update(true, "", false);
            broadcast.Pump(1000);
            Assert.Equal(" ", Assert.Single(Texts(messages)));
            broadcast.Update(true, " ", false);
            broadcast.Pump(1000);
            Assert.Collection(Texts(messages), text => Assert.Equal(" ", text), text => Assert.Equal(" ", text));
            broadcast.Update(true, "never sent", false);
            broadcast.Update(false, "never sent", false);
            broadcast.Pump(2000);
            Assert.Equal(2, Texts(messages).Length);
            Assert.False((bool)messages[^1].Arguments[0]);
        }

        private static string[] Texts(List<(string Address, object[] Arguments)> messages) =>
            messages
                .Where(message => message.Address == "/chatbox/input")
                .Select(message => (string)message.Arguments[0])
                .ToArray();
    }
}
