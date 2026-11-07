using System.Collections.Generic;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Sends the queued key transitions without keeping modifier state. See ADR 0040.</summary>
    internal sealed class KeyFeeder
    {
        private readonly HostWindow _window;

        internal KeyFeeder(HostWindow window)
        {
            _window = window;
        }

        internal bool Send(KeyStroke stroke)
        {
            switch (stroke.Kind)
            {
                case KeyStrokeKind.System:
                    return SendSystemSwitch();
                case KeyStrokeKind.ModifierDown:
                    return SendBatch(new List<NativeInput> { KeyInput.Create(stroke.Key, 0) });
                case KeyStrokeKind.ModifierUp:
                    return SendBatch(new List<NativeInput> { KeyInput.Create(stroke.Key, Win32Native.KeyEventKeyUp) });
                default:
                    if (stroke.Key == 0)
                    {
                        HostLog.Warn("the page supplied no virtual key; this key was not sent");
                        return false;
                    }

                    return SendBatch(BuildStroke(stroke.Key, stroke.Shift && !Held(VirtualKey.Shift)));
            }
        }

        private bool SendSystemSwitch()
        {
            bool winHeld = Held(VirtualKey.LeftWin) || Held(VirtualKey.RightWin);
            var strokes = new List<NativeInput>(4);
            if (!winHeld)
                strokes.Add(KeyInput.Create(VirtualKey.LeftWin, 0));
            strokes.Add(KeyInput.Create(VirtualKey.Space, 0));
            strokes.Add(KeyInput.Create(VirtualKey.Space, Win32Native.KeyEventKeyUp));
            if (!winHeld)
                strokes.Add(KeyInput.Create(VirtualKey.LeftWin, Win32Native.KeyEventKeyUp));
            return SendBatch(strokes);
        }

        private bool SendBatch(List<NativeInput> strokes)
        {
            uint sent = Win32Native.SendInput((uint)strokes.Count, strokes.ToArray(), Marshal.SizeOf<NativeInput>());
            if (sent != strokes.Count)
            {
                HostLog.Warn("SendInput sent " + sent + " of " + strokes.Count + " strokes");
                return false;
            }

            _window.DrainKeyboardInput();
            return true;
        }

        private static bool Held(VirtualKey key) => (Win32Native.GetAsyncKeyState((int)key) & 0x8000) != 0;

        private static List<NativeInput> BuildStroke(VirtualKey key, bool shift)
        {
            var strokes = new List<NativeInput>(4);
            if (shift)
                strokes.Add(KeyInput.Create(VirtualKey.Shift, 0));
            strokes.Add(KeyInput.Create(key, 0));
            strokes.Add(KeyInput.Create(key, Win32Native.KeyEventKeyUp));
            if (shift)
                strokes.Add(KeyInput.Create(VirtualKey.Shift, Win32Native.KeyEventKeyUp));
            return strokes;
        }
    }
}
