using System;
using System.Runtime.InteropServices;
using System.Text;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Input;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Holds the foreground for one admitted composer request. See ADR 0050 and ADR 0055.</summary>
    internal sealed class ForegroundLease
    {
        private readonly ForegroundAcquisition _acquisition;

        internal ForegroundLease(IntPtr hostWindow)
        {
            _acquisition = new ForegroundAcquisition(new Platform(), hostWindow);
        }

        internal bool Acquire(Func<bool> valid)
        {
            bool acquired = _acquisition.Acquire(valid);
            HostLog.Info("composer foreground acquisition -> " + acquired);
            return acquired;
        }

        /// <summary>Observes focus only; old work must never reacquire it.</summary>
        internal bool Ensure() => _acquisition.Ensure();

        internal void Restore() => _acquisition.Restore();

        /// <summary>Tries the normal foreground switch once. See ADR 0041.</summary>
        private sealed class Platform : IForegroundPlatform
        {
            public IntPtr Foreground => Win32Native.GetForegroundWindow();
            public IntPtr Focus => Win32Native.GetFocus();

            public WindowIdentity? Identify(IntPtr window)
            {
                if (window == IntPtr.Zero || !Win32Native.IsWindow(window))
                    return null;
                uint thread = Win32Native.GetWindowThreadProcessId(window, out uint process);
                return thread == 0 ? (WindowIdentity?)null : new WindowIdentity(window, process, thread);
            }

            public bool RequestForeground(IntPtr window)
            {
                bool result = Win32Native.SetForegroundWindow(window);
                HostLog.Info("SetForegroundWindow returned " + result + "; actual foreground " + Describe(Foreground));
                return result;
            }

            public void FocusWindow(IntPtr window) => Win32Native.SetFocus(window);

            public bool KeysAndMenusIdle(IntPtr original)
            {
                for (int key = 1; key < 256; key++)
                    if ((Win32Native.GetAsyncKeyState(key) & 0x8000) != 0)
                        return false;
                uint originalThread = Win32Native.GetWindowThreadProcessId(original, out _);
                return originalThread != 0 && MenusIdle(originalThread) && MenusIdle(0);
            }

            private static bool MenusIdle(uint thread)
            {
                var info = new Win32Native.GuiThreadInfo { Size = (uint)Marshal.SizeOf<Win32Native.GuiThreadInfo>() };
                return Win32Native.GetGUIThreadInfo(thread, ref info)
                    && (info.Flags & 0x1c) == 0
                    && info.MenuOwner == IntPtr.Zero
                    && info.MoveSize == IntPtr.Zero;
            }

            public uint InsertAltPair()
            {
                var input = new[]
                {
                    KeyInput.Create(VirtualKey.LeftAlt, 0),
                    KeyInput.Create(VirtualKey.LeftAlt, Win32Native.KeyEventKeyUp),
                };
                uint count = Win32Native.SendInput(2, input, Marshal.SizeOf<NativeInput>());
                HostLog.Info("foreground Alt pair inserted " + count + "/2; input processing is not fenced");
                return count;
            }
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
