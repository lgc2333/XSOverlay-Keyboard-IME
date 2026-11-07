using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Posts TSF profile changes to the host queue. See ADR 0045.</summary>
    internal sealed class ImeProfileNotifications : IDisposable
    {
        private static readonly ImeShim.ProfileChangedCallback ChangedRef = OnChanged;
        private readonly Action _changed;
        private GCHandle _self;
        private IntPtr _watch;

        private ImeProfileNotifications(Action changed)
        {
            _changed = changed;
            _self = GCHandle.Alloc(this);
        }

        internal static ImeProfileNotifications Open(Action changed)
        {
            var notifications = new ImeProfileNotifications(changed);
            try
            {
                notifications._watch = ImeShim.iime_watch_profiles(
                    ChangedRef,
                    GCHandle.ToIntPtr(notifications._self),
                    out int hr
                );
                if (notifications._watch != IntPtr.Zero)
                    return notifications;
                HostLog.Warn("keyboard input notifications are unavailable, hr=0x" + hr.ToString("x"));
                return null;
            }
            finally
            {
                if (notifications._watch == IntPtr.Zero)
                    notifications.Dispose();
            }
        }

        private static void OnChanged(IntPtr user)
        {
            try
            {
                var notifications = (ImeProfileNotifications)GCHandle.FromIntPtr(user).Target;
                notifications._changed();
            }
            catch (Exception ex)
            {
                HostLog.Error("the keyboard input notification failed " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Dispose()
        {
            if (_watch != IntPtr.Zero)
            {
                ImeShim.iime_unwatch_profiles(_watch);
                _watch = IntPtr.Zero;
            }
            if (_self.IsAllocated)
                _self.Free();
        }
    }
}
