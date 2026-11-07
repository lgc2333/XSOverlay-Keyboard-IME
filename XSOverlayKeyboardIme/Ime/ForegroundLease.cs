using System;
using System.Text;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Holds the foreground while keys are fed, then hands it back. See ADR 0014.</summary>
    internal sealed class ForegroundLease
    {
        private readonly IntPtr _hostWindow;
        private IntPtr _previous;

        internal ForegroundLease(IntPtr hostWindow)
        {
            _hostWindow = hostWindow;
        }

        internal bool Acquire()
        {
            // The first take remembers where the foreground came from; a later one must
            // not overwrite it with this host.
            if (_previous == IntPtr.Zero)
            {
                _previous = Win32Native.GetForegroundWindow();
                if (_previous != IntPtr.Zero && _previous != _hostWindow)
                {
                    HostLog.Info("taking the foreground from " + Describe(_previous));
                }
            }

            return Ensure();
        }

        /// <summary>Re-asserts only when the foreground or the host focus was lost.</summary>
        internal bool Ensure()
        {
            if (Win32Native.GetForegroundWindow() == _hostWindow && Win32Native.GetFocus() == _hostWindow)
            {
                return true;
            }

            return Take();
        }

        internal void Restore()
        {
            if (_previous == IntPtr.Zero)
            {
                return;
            }

            if (Win32Native.GetForegroundWindow() != _hostWindow)
            {
                _previous = IntPtr.Zero;
                return;
            }

            if (Win32Native.SetForegroundWindow(_previous))
            {
                HostLog.Info("foreground restored");
            }
            else
            {
                HostLog.Warn("foreground restore failed; the previous window is gone");
            }

            _previous = IntPtr.Zero;
        }

        /// <summary>Tries the normal foreground switch once. See ADR 0041.</summary>
        private bool Take()
        {
            Win32Native.SetForegroundWindow(_hostWindow);
            if (Win32Native.GetForegroundWindow() == _hostWindow)
            {
                Win32Native.SetFocus(_hostWindow);
            }

            bool taken = Win32Native.GetForegroundWindow() == _hostWindow && Win32Native.GetFocus() == _hostWindow;
            if (!taken)
            {
                HostLog.Warn("could not focus the IME host; this key was not sent");
            }

            return taken;
        }

        internal static string Describe(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return "(none)";
            }

            var className = new StringBuilder(128);
            Win32Native.GetClassNameW(hwnd, className, className.Capacity);
            var module = new StringBuilder(256);
            Win32Native.GetWindowModuleFileNameW(hwnd, module, (uint)module.Capacity);
            return "0x" + hwnd.ToInt64().ToString("x") + " class=" + className + " module=" + module;
        }
    }
}
