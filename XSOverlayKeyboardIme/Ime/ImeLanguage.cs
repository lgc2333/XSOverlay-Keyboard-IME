using System;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The keyboard layout this thread and the foreground window use, and the way to move
    /// this thread onto another one. See ADR 0030.
    /// </summary>
    internal static class ImeLanguage
    {
        /// <summary>The layout this thread composes with; zero when unreadable.</summary>
        internal static IntPtr CurrentLayout()
        {
            return Win32Native.GetKeyboardLayout(0);
        }

        /// <summary>The layout the foreground window uses; zero when unreadable.</summary>
        internal static IntPtr ForegroundLayout()
        {
            IntPtr window = Win32Native.GetForegroundWindow();
            if (window == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            uint thread = Win32Native.GetWindowThreadProcessId(window, IntPtr.Zero);
            return thread == 0 ? IntPtr.Zero : Win32Native.GetKeyboardLayout(thread);
        }

        /// <summary>The language half of a layout handle.</summary>
        internal static ushort Language(IntPtr layout)
        {
            return layout == IntPtr.Zero ? (ushort)0 : (ushort)(layout.ToInt64() & 0xFFFF);
        }

        /// <summary>Puts this thread on a layout, which also picks its default IME.</summary>
        internal static void ActivateLayout(IntPtr layout)
        {
            if (layout != IntPtr.Zero)
            {
                Win32Native.ActivateKeyboardLayout(layout, 0);
            }
        }
    }
}
