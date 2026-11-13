using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests;

public sealed class FocusAdmissionTests
{
    [Fact]
    public void Reopen_requires_current_clear_ack_and_a_new_explicit_request()
    {
        var gate = new FocusAdmission();
        var clear = gate.Bind("page-a");
        var first = new FocusTicket("page-a", clear.Revision, 1);
        Assert.False(gate.TryAcquire(first));
        Assert.True(gate.Acknowledge(clear));
        Assert.True(gate.TryAcquire(first));
        Assert.True(gate.Complete(first));
        Assert.True(gate.Allows(first));
        Assert.True(gate.Revoke(out var next));
        Assert.False(gate.Allows(first));
        Assert.False(gate.Revoke(out _));
        Assert.False(gate.Acknowledge(clear));
        Assert.True(gate.Acknowledge(next));
        Assert.False(gate.TryAcquire(first));
        var second = new FocusTicket("page-a", next.Revision, 2);
        Assert.True(gate.TryAcquire(second));
        Assert.True(gate.Complete(second));
        Assert.False(gate.Acknowledge(next));
        Assert.True(gate.Allows(second));
    }

    [Fact]
    public void Cancel_before_request_prevents_late_request_and_old_cancel_cannot_close_new_hold()
    {
        var gate = new FocusAdmission();
        var clear = gate.Bind("page-a");
        gate.Acknowledge(clear);
        var request = new FocusTicket("page-a", clear.Revision, 1);
        Assert.True(gate.Cancel(request, out var next));
        Assert.False(gate.TryAcquire(request));
        gate.Acknowledge(next);
        var fresh = new FocusTicket("page-a", next.Revision, 2);
        Assert.True(gate.TryAcquire(fresh));
        Assert.True(gate.Complete(fresh));
        Assert.False(gate.Cancel(request, out _));
        Assert.True(gate.Allows(fresh));
    }

    [Fact]
    public void Reentrant_loss_and_same_webview_reload_invalidate_acquisition_continuations()
    {
        var gate = new FocusAdmission();
        var clear = gate.Bind("old-document");
        gate.Acknowledge(clear);
        var old = new FocusTicket("old-document", clear.Revision, 1);
        gate.TryAcquire(old);
        gate.Revoke(out _);
        Assert.False(gate.IsAcquiring(old));
        Assert.False(gate.Complete(old));
        var replacement = gate.Bind("new-document");
        Assert.False(gate.Acknowledge(clear));
        Assert.False(gate.TryAcquire(old));
        Assert.True(gate.Acknowledge(replacement));
        var fresh = new FocusTicket("new-document", replacement.Revision, 1);
        Assert.True(gate.TryAcquire(fresh));
        Assert.False(gate.TryAcquire(fresh));
        gate.Detach();
        Assert.False(gate.Complete(fresh));
        Assert.Null(gate.Current);
    }

    [Fact]
    public void Submit_revoke_requires_the_current_held_request()
    {
        var gate = new FocusAdmission();
        var clear = gate.Bind("page-a");
        gate.Acknowledge(clear);
        var request = new FocusTicket("page-a", clear.Revision, 1);
        Assert.True(gate.TryAcquire(request));
        Assert.True(gate.Complete(request));

        Assert.False(gate.Revoke(new FocusTicket("page-a", clear.Revision, 2), out _));
        Assert.True(gate.Revoke(request, out var next));
        Assert.Equal(clear.Revision + 1, next.Revision);
        Assert.False(gate.Allows(request));
    }
}
