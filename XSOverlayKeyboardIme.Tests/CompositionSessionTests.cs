using XSOverlayKeyboardIme.Composition;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public sealed class CompositionSessionTests
    {
        [Fact]
        public void A_fresh_session_is_idle()
        {
            var session = new CompositionSession();

            Assert.Equal(CompositionPhase.Idle, session.Phase);
        }

        [Fact]
        public void Beat_reports_missing_composition_when_the_ime_never_answered()
        {
            var session = new CompositionSession();

            Assert.Equal(CompositionMove.WaitForComposition, session.BeginTyping());
            Assert.Equal(CompositionMove.ReportNotComposing, session.Beat());
            Assert.Equal(CompositionPhase.Idle, session.Phase);
        }

        [Fact]
        public void Beat_stays_quiet_when_the_ime_already_started_composing()
        {
            var session = new CompositionSession();

            session.BeginTyping();
            session.ElementBegun(7);

            Assert.Equal(CompositionMove.None, session.Beat());
            Assert.Equal(7u, session.ElementId);
        }

        [Fact]
        public void ElementBegun_asks_for_the_composition_text_and_the_candidates()
        {
            var session = new CompositionSession();

            session.BeginTyping();

            Assert.Equal(CompositionMove.ReadCompositionAndCandidates, session.ElementBegun(7));
            Assert.Equal(CompositionPhase.Composing, session.Phase);
            Assert.Equal(7u, session.ElementId);
        }

        [Fact]
        public void Commit_waits_for_the_ime_to_end_the_composition()
        {
            var session = new CompositionSession();

            session.BeginTyping();
            session.ElementBegun(7);

            Assert.True(session.Commit());
            Assert.Equal(CompositionPhase.AwaitingResult, session.Phase);
            Assert.Equal(CompositionMove.ReadResult, session.ElementEnded(7));
            Assert.Equal(CompositionPhase.Finished, session.Phase);
        }

        [Fact]
        public void Beat_reads_the_result_when_the_ime_never_ended_the_composition()
        {
            var session = new CompositionSession();

            session.BeginTyping();
            session.ElementBegun(7);
            session.Commit();

            Assert.Equal(CompositionMove.ReadResult, session.Beat());
            Assert.Equal(CompositionPhase.Finished, session.Phase);
        }

        [Fact]
        public void ElementEnded_is_ignored_once_the_session_finished()
        {
            var session = new CompositionSession();

            session.BeginTyping();
            session.ElementBegun(7);
            session.Commit();
            session.ElementEnded(7);

            Assert.Equal(CompositionMove.None, session.ElementEnded(7));
        }

        [Fact]
        public void Typing_again_restarts_after_a_failed_attempt()
        {
            var session = new CompositionSession();

            session.BeginTyping();
            session.Beat();

            Assert.Equal(CompositionMove.WaitForComposition, session.BeginTyping());
        }
    }
}
