using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>A short-lived Shell observer, created only with the host in front. See ADR 0042.</summary>
    internal sealed class ShellModeButton : IDisposable
    {
        private static readonly ImeShim.ProfileChangedCallback ChangedRef = OnChanged;
        private readonly Action _changed;
        private GCHandle _self;
        private IntPtr _handle;

        private ShellModeButton(Action changed)
        {
            _changed = changed;
            _self = GCHandle.Alloc(this);
        }

        internal static ShellModeButton Open(IntPtr host, Action changed)
        {
            var button = new ShellModeButton(changed);
            try
            {
                button._handle = ImeShim.iime_shell_mode_open(
                    host,
                    ChangedRef,
                    GCHandle.ToIntPtr(button._self),
                    out int hr
                );
                if (button._handle != IntPtr.Zero)
                    return button;
                HostLog.Warn("the Shell mode button is unavailable, hr=0x" + hr.ToString("x"));
                return null;
            }
            finally
            {
                if (button._handle == IntPtr.Zero)
                    button.Dispose();
            }
        }

        internal bool TryRead(out InputModeView view)
        {
            view = null;
            int hr = ImeShim.iime_shell_mode_read(_handle, out ImeShim.ShellModeItem item);
            try
            {
                if (hr < 0)
                    return false;
                string glyph = Marshal.PtrToStringUni(item.Glyph) ?? string.Empty;
                string tooltip = Marshal.PtrToStringUni(item.Tooltip) ?? string.Empty;
                // US returns hidden=0 with every content field empty.
                if (item.Hidden != 0 || (item.Icon == IntPtr.Zero && glyph.Length == 0 && tooltip.Length == 0))
                    return true;
                // Shell's queued click has no reliable completion boundary before foreground return.
                view = new InputModeView(
                    item.Icon == IntPtr.Zero ? null : IconPixels.Read(item.Icon),
                    glyph,
                    "Segoe Fluent Icons",
                    false,
                    tooltip
                );
                return true;
            }
            finally
            {
                ImeShim.iime_shell_mode_free(ref item);
            }
        }

        private static void OnChanged(IntPtr user)
        {
            try
            {
                ((ShellModeButton)GCHandle.FromIntPtr(user).Target)._changed();
            }
            catch (Exception ex)
            {
                HostLog.Error("the Shell mode notification failed " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                ImeShim.iime_shell_mode_close(_handle);
                _handle = IntPtr.Zero;
            }
            if (_self.IsAllocated)
                _self.Free();
        }
    }
}
