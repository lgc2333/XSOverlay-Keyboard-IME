using System;
using System.Runtime.InteropServices;
using System.Text;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>Reads the composing string and the control text. See ADR 0006 and ADR 0013.</summary>
    internal static class ImmTextReader
    {
        internal static string ReadComposition(IntPtr richWindow)
        {
            return ReadImmString(richWindow, Win32Native.GcsCompStr);
        }

        /// <summary>The last commit the IME reported; the control text usually holds it too.</summary>
        internal static string ReadResult(IntPtr richWindow)
        {
            return ReadImmString(richWindow, Win32Native.GcsResultStr);
        }

        /// <summary>The control text as the page will receive it. See ADR 0006 for the reads.</summary>
        internal static string ReadWindowText(IntPtr richWindow)
        {
            var buffer = new StringBuilder(1024);
            Win32Native.GetWindowTextW(richWindow, buffer, buffer.Capacity);
            return buffer.ToString().Replace((char)13, ' ').Replace((char)10, ' ');
        }

        /// <summary>Decodes exactly the byte count the call reports, so no stale memory leaks in.</summary>
        private static string ReadImmString(IntPtr richWindow, int index)
        {
            IntPtr context = Win32Native.ImmGetContext(richWindow);
            if (context == IntPtr.Zero)
            {
                return string.Empty;
            }

            IntPtr buffer = IntPtr.Zero;
            try
            {
                int byteCount = Win32Native.ImmGetCompositionStringW(context, index, IntPtr.Zero, 0);
                if (byteCount <= 0)
                {
                    return string.Empty;
                }

                buffer = Marshal.AllocHGlobal(byteCount);
                int written = Win32Native.ImmGetCompositionStringW(context, index, buffer, byteCount);
                if (written <= 0)
                {
                    return string.Empty;
                }

                var bytes = new byte[written];
                Marshal.Copy(buffer, bytes, 0, written);
                return Encoding.Unicode.GetString(bytes);
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }

                Win32Native.ImmReleaseContext(richWindow, context);
            }
        }
    }
}
