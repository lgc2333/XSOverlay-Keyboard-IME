using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Sends keys to the IME through global SendInput. The IME belongs to the
    /// foreground thread, so the lease is re-asserted on every stroke.
    /// </summary>
    internal sealed class KeyFeeder
    {
        private const int StrokePauseMilliseconds = 120;
        private readonly HostWindow _window;
        private readonly ForegroundLease _lease;

        internal KeyFeeder(HostWindow window, ForegroundLease lease)
        {
            _window = window;
            _lease = lease;
        }

        internal bool Send(VirtualKey key, bool shift)
        {
            _lease.Ensure();
            _lease.BeginAttach();

            bool sent;
            try
            {
                sent = SendAttached(key, shift);
            }
            finally
            {
                _lease.EndAttach();
            }

            // Only after detaching: while attached, the peek would read the shared queue
            // and dispatch another thread's window procedures on this one.
            if (sent)
            {
                _window.DrainKeyboardInput();
            }

            return sent;
        }

        private bool SendAttached(VirtualKey key, bool shift)
        {
            var strokes = new List<NativeInput>(4);
            if (shift)
            {
                strokes.Add(Create(VirtualKey.Shift, 0));
            }

            strokes.Add(Create(key, 0));
            strokes.Add(Create(key, Win32Native.KeyEventKeyUp));
            if (shift)
            {
                strokes.Add(Create(VirtualKey.Shift, Win32Native.KeyEventKeyUp));
            }

            uint sent = Win32Native.SendInput((uint)strokes.Count, strokes.ToArray(), Marshal.SizeOf<NativeInput>());
            Thread.Sleep(StrokePauseMilliseconds);

            if (sent != strokes.Count)
            {
                HostLog.Warn("SendInput sent " + sent + " of " + strokes.Count + " strokes");
                return false;
            }

            return true;
        }

        /// <summary>Types literal text; characters the layout cannot reach are skipped.</summary>
        internal bool Send(string text)
        {
            foreach (char character in text)
            {
                short scan = Win32Native.VkKeyScanW(character);
                if (scan == -1)
                {
                    HostLog.Warn("layout cannot type '" + character + "'");
                    return false;
                }

                bool shift = (scan & 0x100) != 0;
                if (!Send((VirtualKey)(scan & 0xFF), shift))
                {
                    return false;
                }
            }

            return true;
        }

        private static NativeInput Create(VirtualKey key, uint flags)
        {
            var input = new NativeInput { Type = Win32Native.InputKeyboard };
            input.Data.Keyboard = new KeyboardInput
            {
                VirtualKey = (ushort)key,
                ScanCode = (ushort)Win32Native.MapVirtualKeyW((uint)key, Win32Native.MapVirtualKeyToScan),
                Flags = flags,
            };
            return input;
        }
    }
}
