using System;
using System.Runtime.InteropServices;
using System.Text;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>Win32 entry points and constants the host needs.</summary>
    internal static class Win32Native
    {
        internal const uint WmDestroy = 0x0002;
        internal const uint WmClose = 0x0010;
        internal const uint WmChar = 0x0102;
        internal const uint WmApp = 0x8000;
        internal const uint WmInputLangChange = 0x0051;
        internal const uint WmTimer = 0x0113;
        internal const uint WmKeyFirst = 0x0100;
        internal const uint WmKeyLast = 0x0109;

        internal const uint WsPopup = 0x80000000;
        internal const uint WsVisible = 0x10000000;
        internal const uint WsExTopmost = 0x00000008;
        internal const uint WsExToolWindow = 0x00000080;
        internal const int SwShow = 5;

        internal const uint InputKeyboard = 1;
        internal const uint KeyEventKeyUp = 0x0002;
        internal const uint MapVirtualKeyToScan = 0;

        internal const uint CoInitApartmentThreaded = 0x2;

        /// <summary>LOCALE_SLOCALIZEDDISPLAYNAME, the language's own name.</summary>
        internal const uint LocaleDisplayName = 0x00000002;
        internal const uint ClsCtxInProcServer = 0x1;

        internal const uint SemFailCriticalErrors = 0x0001;
        internal const uint SemNoOpenFileErrorBox = 0x8000;

        internal delegate IntPtr WndProcDelegate(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetModuleHandleW(string name);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        internal static extern uint SetErrorMode(uint mode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern ushort RegisterClassExW(ref WndClassEx windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateWindowExW(
            uint exStyle,
            string className,
            string windowName,
            uint style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr param
        );

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr DefWindowProcW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hwnd, int command);

        [DllImport("user32.dll")]
        internal static extern bool UpdateWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassNameW(IntPtr hwnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        internal static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetMessageW(out Msg message, IntPtr hwnd, uint min, uint max);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool PeekMessageW(out Msg message, IntPtr hwnd, uint min, uint max, uint remove);

        [DllImport("user32.dll")]
        internal static extern bool TranslateMessage(ref Msg message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr DispatchMessageW(ref Msg message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool PostMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetTimer(IntPtr hwnd, IntPtr id, uint elapse, IntPtr procedure);

        [DllImport("user32.dll")]
        internal static extern bool KillTimer(IntPtr hwnd, IntPtr id);

        [DllImport("user32.dll")]
        internal static extern void PostQuitMessage(int code);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern void SwitchToThisWindow(IntPtr hwnd, bool altTab);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetFocus();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint GetWindowModuleFileNameW(IntPtr hwnd, StringBuilder path, uint maxCount);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetFocus(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr processId);

        [DllImport("user32.dll")]
        internal static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachState);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint count, NativeInput[] inputs, int size);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern short VkKeyScanW(char character);

        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKeyW(uint code, uint mapType);

        /// <summary>
        /// Whether a key is down right now. The high bit is set while it is held; the low
        /// bit says it was pressed since the last call and is ignored here.
        /// </summary>
        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int key);

        /// <summary>The name of a language in the user's own words, for a layout with no profile.</summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetLocaleInfoW(uint locale, uint field, StringBuilder buffer, int size);

        [DllImport("ole32.dll")]
        internal static extern int CoInitializeEx(IntPtr reserved, uint coInit);

        [DllImport("ole32.dll")]
        internal static extern void CoUninitialize();

        /// <summary>
        /// The TSF thread manager for the calling thread, created on first use. The plugin
        /// only reads the language bar out of it. See ADR 0028.
        /// </summary>
        [DllImport("msctf.dll")]
        internal static extern int TF_GetThreadMgr(out IntPtr threadManager);

        [DllImport("ole32.dll")]
        internal static extern int CoCreateInstance(
            ref Guid clsid,
            IntPtr outer,
            uint context,
            ref Guid iid,
            out IntPtr instance
        );

        [StructLayout(LayoutKind.Sequential)]
        internal struct Msg
        {
            internal IntPtr Hwnd;
            internal uint Message;
            internal IntPtr WParam;
            internal IntPtr LParam;
            internal uint Time;
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WndClassEx
        {
            internal uint CbSize;
            internal uint Style;
            internal IntPtr LpfnWndProc;
            internal int CbClsExtra;
            internal int CbWndExtra;
            internal IntPtr HInstance;
            internal IntPtr HIcon;
            internal IntPtr HCursor;
            internal IntPtr HbrBackground;
            internal string LpszMenuName;
            internal string LpszClassName;
            internal IntPtr HIconSm;
        }
    }
}
