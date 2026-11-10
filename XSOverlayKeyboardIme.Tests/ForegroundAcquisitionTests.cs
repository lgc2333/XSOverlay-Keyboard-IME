using System;
using XSOverlayKeyboardIme.Takeover;
using Xunit;

namespace XSOverlayKeyboardIme.Tests;

public sealed class ForegroundAcquisitionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Normal_request_is_not_blocked_by_pressed_keys_or_menus(bool succeeds)
    {
        var api = new Platform { NormalSucceeds = succeeds, Idle = false };
        var lease = new ForegroundAcquisition(api, api.Host);
        Assert.Equal(succeeds, lease.Acquire(() => true));
        Assert.Equal(1, api.Requests);
        Assert.Equal(0, api.Alt);
        Assert.Equal(succeeds, lease.Ensure());
        Assert.Equal(1, api.Requests);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 1, false)]
    [InlineData(2, 2, true)]
    public void Denied_normal_allows_only_one_complete_alt_batch(uint inserted, int calls, bool success)
    {
        var api = new Platform { Inserted = inserted };
        var lease = new ForegroundAcquisition(api, api.Host);
        Assert.Equal(success, lease.Acquire(() => true));
        Assert.Equal(calls, api.Requests);
        Assert.Equal(1, api.Alt);
    }

    [Fact]
    public void Reentrant_revoke_prevents_alt_and_lost_focus_never_reacquires()
    {
        bool valid = true;
        var api = new Platform { OnRequest = () => valid = false };
        var lease = new ForegroundAcquisition(api, api.Host);
        Assert.False(lease.Acquire(() => valid));
        Assert.Equal(0, api.Alt);
        api.Foreground = (IntPtr)99;
        Assert.False(lease.Ensure());
        lease.Restore();
        Assert.Equal(1, api.Requests);
    }

    [Fact]
    public void Unobserved_success_is_not_a_denial_and_cannot_enable_alt()
    {
        var api = new Platform { SuccessWithoutActivation = true };
        var lease = new ForegroundAcquisition(api, api.Host);
        Assert.False(lease.Acquire(() => true));
        Assert.Equal(1, api.Requests);
        Assert.Equal(0, api.Alt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Invalidated_request_during_idle_check_cannot_insert_alt(int change)
    {
        bool valid = true;
        var api = new Platform();
        api.OnIdle = () =>
        {
            if (change == 0)
                valid = false;
            else if (change == 1)
                api.Foreground = (IntPtr)99;
            else
                api.OriginalIdentityValid = false;
        };
        var lease = new ForegroundAcquisition(api, api.Host);

        Assert.False(lease.Acquire(() => valid));
        Assert.Equal(0, api.Alt);
        Assert.Equal(1, api.Requests);
    }

    [Fact]
    public void Restore_is_once_and_only_while_host_foreground_and_original_identity_valid()
    {
        var api = new Platform { NormalSucceeds = true };
        var lease = new ForegroundAcquisition(api, api.Host);
        Assert.True(lease.Acquire(() => true));
        lease.Restore();
        lease.Restore();
        Assert.Equal(2, api.Requests);
        Assert.Equal((IntPtr)1, api.Foreground);
        Assert.True(lease.Acquire(() => true));
        api.OriginalIdentityValid = false;
        lease.Restore();
        Assert.Equal(3, api.Requests);
    }

    private sealed class Platform : IForegroundPlatform
    {
        public IntPtr Host { get; } = (IntPtr)2;
        public IntPtr Foreground { get; set; } = (IntPtr)1;
        public IntPtr Focus { get; set; }
        internal bool NormalSucceeds;
        internal bool SuccessWithoutActivation;
        internal bool Idle = true;
        internal bool OriginalIdentityValid = true;
        internal uint Inserted = 2;
        internal int Requests;
        internal int Alt;
        internal Action OnRequest;
        internal Action OnIdle;

        public WindowIdentity? Identify(IntPtr window) =>
            window == (IntPtr)1 && !OriginalIdentityValid ? null : new WindowIdentity(window, 10, 20);

        public bool RequestForeground(IntPtr window)
        {
            Requests++;
            OnRequest?.Invoke();
            if (SuccessWithoutActivation)
                return true;
            bool success = window != Host || NormalSucceeds || Requests == 2;
            if (success)
                Foreground = window;
            return success;
        }

        public void FocusWindow(IntPtr window) => Focus = window;

        public bool KeysAndMenusIdle(IntPtr original)
        {
            OnIdle?.Invoke();
            return Idle;
        }

        public uint InsertAltPair()
        {
            Alt++;
            return Inserted;
        }
    }
}
