using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>Owns the hidden host window and pumps the message loop. See ADR 0005 and ADR 0006.</summary>
    internal sealed class HostWindow
    {
        internal const uint BeatMessage = Win32Native.WmApp + 1;
        internal const uint ElementMessage = Win32Native.WmApp + 2;
        internal const uint ObserveMessage = Win32Native.WmApp + 3;

        private const string WindowClassName = "XSOverlayKeyboardImeHost";
        private const string EditorClassName = "RICHEDIT50W";
        private const string WindowTitle = "XSOverlay Keyboard IME host";
        private const uint WsPopup = 0x80000000;
        private const uint WsExTopmost = 0x00000008;
        private const uint WsExToolWindow = 0x00000080;
        private const int OffscreenCoordinate = -32000;
        private const int BackgroundBrush = 6;

        // The window procedure is a static entry point, so exactly one host window
        // may exist per process; the plugin never needs a second one.
        private static readonly Win32Native.WndProcDelegate WindowProcedureRef = WindowProcedure;
        private static HostWindow _current;

        private readonly Action<uint> _onHostMessage;
        private IntPtr _handle;
        private IntPtr _editor;

        private HostWindow(Action<uint> onHostMessage)
        {
            _onHostMessage = onHostMessage;
        }

        internal IntPtr Handle
        {
            get { return _handle; }
        }

        internal IntPtr Editor
        {
            get { return _editor; }
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

        internal void Close()
        {
            if (_handle != IntPtr.Zero)
            {
                Win32Native.DestroyWindow(_handle);
                _handle = IntPtr.Zero;
                _editor = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Lives off screen with no taskbar entry. It still has to be shown and focused,
        /// because the IME only composes into the foreground window.
        /// </summary>
        /// <summary>Drains queued messages, so the window is responsive before it takes the foreground.</summary>
        internal void PumpPending()
        {
            const uint PmRemove = 1;
            int guard = 0;
            while (guard++ < 200 && Win32Native.PeekMessageW(out Win32Native.Msg message, IntPtr.Zero, 0, 0, PmRemove))
            {
                Win32Native.TranslateMessage(ref message);
                Win32Native.DispatchMessageW(ref message);
            }
        }

        private void CreateNative()
        {
            Win32Native.LoadLibraryW("msftedit.dll");
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
                WsExTopmost,
                WindowClassName,
                WindowTitle,
                Win32Native.WsOverlappedWindow | Win32Native.WsVisible,
                40,
                40,
                480,
                160,
                IntPtr.Zero,
                IntPtr.Zero,
                instance,
                IntPtr.Zero
            );

            _editor = Win32Native.CreateWindowExW(
                0,
                EditorClassName,
                string.Empty,
                Win32Native.WsChild | Win32Native.WsVisible | Win32Native.WsVScroll | Win32Native.EsMultiline,
                8,
                8,
                448,
                104,
                _handle,
                IntPtr.Zero,
                instance,
                IntPtr.Zero
            );

            Win32Native.ShowWindow(_handle, Win32Native.SwShow);
            Win32Native.UpdateWindow(_handle);

            if (_handle == IntPtr.Zero || _editor == IntPtr.Zero)
            {
                throw new InvalidOperationException("host window creation failed");
            }

            HostLog.Info("host window ready, off screen");
        }

        private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                HostWindow window = _current;
                if (window != null && message >= Win32Native.WmApp)
                {
                    window._onHostMessage(message);
                    return IntPtr.Zero;
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
