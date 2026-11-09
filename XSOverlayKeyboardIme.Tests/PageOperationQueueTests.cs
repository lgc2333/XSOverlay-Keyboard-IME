using System;
using System.Collections.Generic;
using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests
{
    public class PageOperationQueueTests
    {
        [Fact]
        public void LaterEditWaitsForActualTextCompletionAndDuplicateAckCannotAdvanceIt()
        {
            var callbacks = new Queue<Action>();
            var delivered = new List<string>();
            Action<bool> text = null,
                edit = null;
            var queue = new PageOperationQueue(callbacks.Enqueue, () => Assert.Fail("unexpected failure"), () => { });
            queue.Enqueue(done =>
            {
                delivered.Add("text");
                text = done;
            });
            queue.Enqueue(done =>
            {
                delivered.Add("edit");
                edit = done;
            });
            Assert.Equal("text", Assert.Single(delivered));
            text(true);
            Assert.Equal("text", Assert.Single(delivered));
            callbacks.Dequeue()();
            Assert.Collection(delivered, value => Assert.Equal("text", value), value => Assert.Equal("edit", value));
            text(true);
            callbacks.Dequeue()();
            Assert.True(queue.Pending);
            edit(true);
            callbacks.Dequeue()();
            Assert.False(queue.Pending);
        }

        [Fact]
        public void RevocationIsolatesOldCompletionFromTheNextFocusPeriod()
        {
            var callbacks = new Queue<Action>();
            Action<bool> old = null,
                current = null;
            int failures = 0;
            var queue = new PageOperationQueue(callbacks.Enqueue, () => failures++, () => { });
            queue.Enqueue(done => old = done);
            queue.Enqueue(_ => Assert.Fail("revoked edit ran"));
            queue.Cancel();
            queue.Enqueue(done => current = done);
            old(false);
            callbacks.Dequeue()();
            Assert.True(queue.Pending);
            Assert.Equal(0, failures);
            current(true);
            callbacks.Dequeue()();
            Assert.False(queue.Pending);
        }

        [Fact]
        public void FailedOrThrowingOperationDropsItsTailWithoutRetry()
        {
            var callbacks = new Queue<Action>();
            int failures = 0,
                calls = 0;
            var queue = new PageOperationQueue(callbacks.Enqueue, () => failures++, () => { });
            queue.Enqueue(_ =>
            {
                calls++;
                throw new InvalidOperationException();
            });
            queue.Enqueue(_ => Assert.Fail("failed edit tail ran"));
            callbacks.Dequeue()();
            Assert.False(queue.Pending);
            Assert.Equal(1, failures);
            Assert.Equal(1, calls);
        }
    }
}
