using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>Holds the foreground while keys are fed, then hands it back. See ADR 0006.</summary>
    internal sealed class ForegroundLease
    {
        private const int TakeAttempts = 5;
        private const int RetryPauseMilliseconds = 40;
        private const ushort VirtualKeyMenu = 0x12;

        private readonly IntPtr _hostWindow;
        private readonly IntPtr _focusWindow;
        private IntPtr _previous;
        private uint _attachedThread;

        internal ForegroundLease(IntPtr hostWindow, IntPtr focusWindow)
        {
            _hostWindow = hostWindow;
            _focusWindow = focusWindow;
        }

        internal void Acquire()
        {
            _previous = Win32Native.GetForegroundWindow();
            if (_previous != IntPtr.Zero && _previous != _hostWindow)
            {
                HostLog.Info("taking the foreground from " + Describe(_previous));
            }

            Take();
        }

        /// <summary>Re-asserts only when the foreground or the editor focus was lost.</summary>
        internal void Ensure()
        {
            if (Win32Native.GetForegroundWindow() == _hostWindow && Win32Native.GetFocus() == _focusWindow)
            {
                return;
            }

            Take();
        }

        /// <summary>
        /// Joins the foreground thread's input queue for a key burst, so injected keys
        /// reach the focused host window while another window owns the foreground.
        /// </summary>
        internal void BeginAttach()
        {
            if (_attachedThread != 0)
            {
                return;
            }

            IntPtr front = Win32Native.GetForegroundWindow();
            if (front == IntPtr.Zero)
            {
                return;
            }

            uint frontThread = Win32Native.GetWindowThreadProcessId(front, IntPtr.Zero);
            uint ownThread = Win32Native.GetCurrentThreadId();
            if (frontThread == 0 || frontThread == ownThread)
            {
                return;
            }

            if (Win32Native.AttachThreadInput(ownThread, frontThread, true))
            {
                _attachedThread = frontThread;
            }
        }

        internal void EndAttach()
        {
            if (_attachedThread == 0)
            {
                return;
            }

            Win32Native.AttachThreadInput(Win32Native.GetCurrentThreadId(), _attachedThread, false);
            _attachedThread = 0;
        }

        internal void Restore()
        {
            if (_previous == IntPtr.Zero)
            {
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

        /// <summary>
        /// Windows only lets the foreground window receive injected keys. Attaching to
        /// that window's input queue plus a synthetic Alt press is what makes the switch
        /// legal without user input.
        /// </summary>
        private void Take()
        {
            uint ownThread = Win32Native.GetCurrentThreadId();
            IntPtr front = Win32Native.GetForegroundWindow();
            uint frontThread = front == IntPtr.Zero ? 0 : Win32Native.GetWindowThreadProcessId(front, IntPtr.Zero);

            if (frontThread != 0)
            {
                Win32Native.AttachThreadInput(ownThread, frontThread, true);
            }

            TapAlt();

            for (int attempt = 0; attempt < TakeAttempts; attempt++)
            {
                Win32Native.SetForegroundWindow(_hostWindow);
                if (Win32Native.GetForegroundWindow() == _hostWindow)
                {
                    break;
                }

                Win32Native.SwitchToThisWindow(_hostWindow, false);
                Thread.Sleep(RetryPauseMilliseconds);
            }

            IntPtr focus = Win32Native.SetFocus(_focusWindow);

            if (frontThread != 0)
            {
                Win32Native.AttachThreadInput(ownThread, frontThread, false);
            }

            bool taken = Win32Native.GetForegroundWindow() == _hostWindow;
            HostLog.Info(
                "foreground taken="
                    + taken
                    + " focus="
                    + (focus == _focusWindow)
                    + " held by "
                    + Describe(Win32Native.GetForegroundWindow())
            );

            if (!taken)
            {
                HostLog.Warn("could not take the foreground; keys will not reach the IME");
            }
        }

        /// <summary>The system treats a synthetic Alt press as a user gesture and allows the switch.</summary>
        private static void TapAlt()
        {
            var strokes = new[] { CreateKey(VirtualKeyMenu, 0), CreateKey(VirtualKeyMenu, Win32Native.KeyEventKeyUp) };
            Win32Native.SendInput((uint)strokes.Length, strokes, Marshal.SizeOf<NativeInput>());
        }

        private static NativeInput CreateKey(ushort virtualKey, uint flags)
        {
            var input = new NativeInput { Type = Win32Native.InputKeyboard };
            input.Data.Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                ScanCode = (ushort)Win32Native.MapVirtualKeyW(virtualKey, Win32Native.MapVirtualKeyToScan),
                Flags = flags,
            };
            return input;
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
