using System;
using System.Text;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>Reads composition text, IMM first and the control text as a fallback. See ADR 0006.</summary>
    internal static class ImmTextReader
    {
        internal static string ReadComposition(IntPtr richWindow)
        {
            return ReadImmString(richWindow, Win32Native.GcsCompStr);
        }

        internal static string ReadResult(IntPtr richWindow)
        {
            return ReadImmString(richWindow, Win32Native.GcsResultStr);
        }

        internal static string ReadWindowText(IntPtr richWindow)
        {
            var buffer = new StringBuilder(1024);
            Win32Native.GetWindowTextW(richWindow, buffer, buffer.Capacity);
            return buffer.ToString().Replace((char)13, ' ').Replace((char)10, ' ');
        }

        private static string ReadImmString(IntPtr richWindow, int index)
        {
            IntPtr context = Win32Native.ImmGetContext(richWindow);
            if (context == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                int byteCount = Win32Native.ImmGetCompositionStringW(context, index, null, 0);
                if (byteCount <= 0)
                {
                    return string.Empty;
                }

                var buffer = new StringBuilder(byteCount / 2 + 2);
                Win32Native.ImmGetCompositionStringW(context, index, buffer, byteCount);
                return buffer.ToString();
            }
            finally
            {
                Win32Native.ImmReleaseContext(richWindow, context);
            }
        }
    }
}
