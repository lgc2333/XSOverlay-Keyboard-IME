using System;
using System.Runtime.InteropServices;
using System.Text;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The hidden window the IME composes into, and the message pump that feeds it.
    /// Text the IME does not compose arrives here as WM_CHAR. See ADR 0005 and ADR 0022.
    /// </summary>
    internal sealed class HostWindow
    {
        internal const uint StrokeMessage = Win32Native.WmApp + 1;

        private const string WindowClassName = "XSOverlayKeyboardImeHost";
        private const string WindowTitle = "XSOverlay Keyboard IME host";
        private const int OffscreenCoordinate = -32000;
        private const int BackgroundBrush = 6;
        private const int DrainLimit = 512;

        // The window procedure is a static entry point, so exactly one host window
        // may exist per process; the plugin never needs a second one.
        private static readonly Win32Native.WndProcDelegate WindowProcedureRef = WindowProcedure;
        private static HostWindow _current;

        private readonly Action<uint> _onHostMessage;
        private readonly StringBuilder _typed = new StringBuilder();
        private IntPtr _handle;

        private HostWindow(Action<uint> onHostMessage)
        {
            _onHostMessage = onHostMessage;
        }

        internal IntPtr Handle
        {
            get { return _handle; }
        }

        internal static HostWindow Create(Action<uint> onHostMessage)
        {
            var window = new HostWindow(onHostMessage);
            window.CreateNative();
            _current = window;
            return window;
        }

        internal void Post(uint message)
        {
            Win32Native.PostMessageW(_handle, message, IntPtr.Zero, IntPtr.Zero);
        }

        internal int PumpMessages()
        {
            int result;
            while ((result = Win32Native.GetMessageW(out Win32Native.Msg message, IntPtr.Zero, 0, 0)) > 0)
            {
                Win32Native.TranslateMessage(ref message);
                Win32Native.DispatchMessageW(ref message);
            }

            return result;
        }

        /// <summary>
        /// Takes the characters the IME let through since the last call. Only the host
        /// thread reads or writes them.
        /// </summary>
        internal string TakeTyped()
        {
            if (_typed.Length == 0)
            {
                return string.Empty;
            }

            string text = _typed.ToString();
            _typed.Clear();
            return text;
        }

        internal void Close()
        {
            _current = null;
            if (_handle != IntPtr.Zero)
            {
                Win32Native.DestroyWindow(_handle);
                _handle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Dispatches keyboard messages the injected keys left in this thread's queue.
        /// Posted messages outrank pending input, so a read that follows a send sees
        /// stale state until the input queue is drained.
        /// </summary>
        internal int DrainKeyboardInput()
        {
            return DrainMessages(Win32Native.WmKeyFirst, Win32Native.WmKeyLast);
        }

        private static int DrainMessages(uint first, uint last)
        {
            const uint PmRemove = 1;
            int count = 0;
            while (
                count < DrainLimit
                && Win32Native.PeekMessageW(out Win32Native.Msg message, IntPtr.Zero, first, last, PmRemove)
            )
            {
                Win32Native.TranslateMessage(ref message);
                Win32Native.DispatchMessageW(ref message);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Lives off screen with no taskbar entry. It still has to be shown and focused,
        /// because the IME only composes into the foreground window.
        /// </summary>
        private void CreateNative()
        {
            IntPtr instance = Win32Native.GetModuleHandleW(null);

            var windowClass = new Win32Native.WndClassEx
            {
                CbSize = (uint)Marshal.SizeOf<Win32Native.WndClassEx>(),
                LpfnWndProc = Marshal.GetFunctionPointerForDelegate(WindowProcedureRef),
                HInstance = instance,
                HbrBackground = (IntPtr)(BackgroundBrush + 1),
                LpszClassName = WindowClassName,
            };
            Win32Native.RegisterClassExW(ref windowClass);

            _handle = Win32Native.CreateWindowExW(
                Win32Native.WsExTopmost | Win32Native.WsExToolWindow,
                WindowClassName,
                WindowTitle,
                Win32Native.WsPopup | Win32Native.WsVisible,
                OffscreenCoordinate,
                OffscreenCoordinate,
                480,
                160,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero
            );

            Win32Native.ShowWindow(_handle, Win32Native.SwShow);
            Win32Native.UpdateWindow(_handle);

            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("host window creation failed");
            }

            HostLog.Info("host window ready, off screen");
        }

        /// <summary>
        /// Keeps the text the IME did not compose. A control character belongs to a key
        /// the session already acts on, such as Enter or Backspace, so it is dropped.
        /// </summary>
        private void Collect(IntPtr wParam)
        {
            char character = (char)(wParam.ToInt64() & 0xFFFF);
            if (character >= ' ' && character != '\u007F')
            {
                _typed.Append(character);
            }
        }

        private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                HostWindow window = _current;
                if (window != null)
                {
                    if (message >= Win32Native.WmApp)
                    {
                        window._onHostMessage(message);
                        return IntPtr.Zero;
                    }

                    if (message == Win32Native.WmChar)
                    {
                        window.Collect(wParam);
                        return IntPtr.Zero;
                    }
                }

                if (message == Win32Native.WmClose)
                {
                    Win32Native.DestroyWindow(hwnd);
                    return IntPtr.Zero;
                }

                if (message == Win32Native.WmDestroy)
                {
                    Win32Native.PostQuitMessage(0);
                    return IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                // An exception must never unwind through the native window procedure.
                HostLog.Error("window procedure threw " + ex.GetType().Name + ": " + ex.Message);
            }

            return Win32Native.DefWindowProcW(hwnd, message, wParam, lParam);
        }
    }
}
