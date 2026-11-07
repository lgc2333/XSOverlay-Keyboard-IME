using System.Collections.Generic;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// Sends keys to the IME through global SendInput. The IME belongs to the
    /// foreground thread, so the lease is re-asserted on every stroke.
    /// </summary>
    internal sealed class KeyFeeder
    {
        private readonly HostWindow _window;
        private readonly ForegroundLease _lease;

        // The modifiers the page latched and the host holds down. See ADR 0038.
        private readonly HashSet<VirtualKey> _latched = new HashSet<VirtualKey>();

        internal KeyFeeder(HostWindow window, ForegroundLease lease)
        {
            _window = window;
            _lease = lease;
        }

        /// <summary>True while the host holds a shift down, so a stroke must not lift it.</summary>
        private bool ShiftHeld
        {
            get
            {
                return _latched.Contains(VirtualKey.Shift)
                    || _latched.Contains(VirtualKey.LeftShift)
                    || _latched.Contains(VirtualKey.RightShift);
            }
        }

        internal bool Send(KeyStroke stroke)
        {
            switch (stroke.Kind)
            {
                case KeyStrokeKind.Text:
                    return SendText(stroke.Text);
                case KeyStrokeKind.System:
                    return SendSystemSwitch();
                case KeyStrokeKind.Modifier:
                    return ToggleModifier(stroke.Key);
                default:
                    return Send(stroke.Key, stroke.Shift);
            }
        }

        /// <summary>Sends the system's language switch. See ADR 0036.</summary>
        private bool SendSystemSwitch()
        {
            return SendBatch(
                new List<NativeInput>(4)
                {
                    KeyInput.Create(VirtualKey.LeftWin, 0),
                    KeyInput.Create(VirtualKey.Space, 0),
                    KeyInput.Create(VirtualKey.Space, Win32Native.KeyEventKeyUp),
                    KeyInput.Create(VirtualKey.LeftWin, Win32Native.KeyEventKeyUp),
                }
            );
        }

        /// <summary>
        /// Presses a latched modifier and keeps it down, or lifts it when the page latches it
        /// again. The IME then sees the modifier as genuinely held. See ADR 0038.
        /// </summary>
        private bool ToggleModifier(VirtualKey key)
        {
            var strokes = new List<NativeInput>(1);
            if (_latched.Remove(key))
            {
                HostLog.Info("latch " + key + " up");
                strokes.Add(KeyInput.Create(key, Win32Native.KeyEventKeyUp));
            }
            else
            {
                HostLog.Info("latch " + key + " down");
                _latched.Add(key);
                strokes.Add(KeyInput.Create(key, 0));
            }

            return SendBatch(strokes);
        }

        /// <summary>Lifts every modifier the page latched, for a composer that lost the focus.</summary>
        internal void ReleaseLatchedModifiers()
        {
            if (_latched.Count == 0)
            {
                return;
            }

            var strokes = new List<NativeInput>(_latched.Count);
            foreach (VirtualKey key in _latched)
            {
                HostLog.Info("latch " + key + " up, the composer lost the focus");
                strokes.Add(KeyInput.Create(key, Win32Native.KeyEventKeyUp));
            }

            _latched.Clear();
            SendBatch(strokes);
        }

        private bool Send(VirtualKey key, bool shift)
        {
            // A held shift already applies; sending another would lift the latch on its way out.
            return SendBatch(BuildStroke(key, shift && !ShiftHeld));
        }

        /// <summary>Types one character through the layout, for the keys the page resolved.</summary>
        private bool SendText(char character)
        {
            short scan = Win32Native.VkKeyScanW(character);
            if (scan == -1)
            {
                HostLog.Warn("the layout cannot type '" + character + "'");
                return false;
            }

            return Send((VirtualKey)(scan & 0xFF), (scan & 0x100) != 0);
        }

        /// <summary>
        /// Sends a batch while the lease holds the foreground. No pause follows: the caller
        /// drains this thread's key messages, and the IME answers on this thread while they
        /// are dispatched. See ADR 0026.
        /// </summary>
        private bool SendBatch(List<NativeInput> strokes)
        {
            _lease.Ensure();
            _lease.BeginAttach();

            bool sent;
            try
            {
                sent = SendInputBatch(strokes);
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

        private static bool SendInputBatch(List<NativeInput> strokes)
        {
            uint sent = Win32Native.SendInput((uint)strokes.Count, strokes.ToArray(), Marshal.SizeOf<NativeInput>());

            if (sent != strokes.Count)
            {
                HostLog.Warn("SendInput sent " + sent + " of " + strokes.Count + " strokes");
                return false;
            }

            return true;
        }

        private static List<NativeInput> BuildStroke(VirtualKey key, bool shift)
        {
            var strokes = new List<NativeInput>(4);
            if (shift)
            {
                strokes.Add(KeyInput.Create(VirtualKey.Shift, 0));
            }

            strokes.Add(KeyInput.Create(key, 0));
            strokes.Add(KeyInput.Create(key, Win32Native.KeyEventKeyUp));
            if (shift)
            {
                strokes.Add(KeyInput.Create(VirtualKey.Shift, Win32Native.KeyEventKeyUp));
            }

            return strokes;
        }
    }
}
