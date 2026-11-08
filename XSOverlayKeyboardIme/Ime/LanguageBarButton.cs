using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>The host thread's public input-mode item. See ADR 0042.</summary>
    internal sealed class LanguageBarButton : IInputModeSource
    {
        private readonly ITfThreadMgr _thread;
        private readonly ITfLangBarItem _item;
        private readonly ITfLangBarItemButton _button;

        private LanguageBarButton(ITfThreadMgr thread, ITfLangBarItem item)
        {
            _thread = thread;
            _item = item;
            _button = item as ITfLangBarItemButton;
        }

        internal static LanguageBarButton Open(out bool absent)
        {
            absent = false;
            int hr = Win32Native.TF_GetThreadMgr(out IntPtr raw);
            if (hr < 0 || raw == IntPtr.Zero)
                return null;
            ITfThreadMgr thread;
            try
            {
                thread = Marshal.GetObjectForIUnknown(raw) as ITfThreadMgr;
            }
            finally
            {
                Marshal.Release(raw);
            }
            bool retained = false;
            try
            {
                var manager = thread as ITfLangBarItemMgr;
                if (manager == null)
                    return null;
                var id = new Guid("2c77a81e-41cc-4178-a3a7-5f8a987568e6");
                hr = manager.GetItem(ref id, out ITfLangBarItem item);
                // S_FALSE means absence. Query and picture failures do not.
                absent = hr == 1 && item == null;
                if (hr != 0 || item == null)
                {
                    Release(item);
                    return null;
                }
                retained = true;
                return new LanguageBarButton(thread, item);
            }
            finally
            {
                if (!retained)
                    Release(thread);
            }
        }

        internal InputModeView Read()
        {
            int hr = _item.GetStatus(out uint status);
            if (hr >= 0 && (status & 1) != 0)
                return null;
            bool enabled = hr >= 0 && (status & 2) == 0 && _button != null;
            string tooltip = string.Empty;
            if (_item.GetTooltipString(out IntPtr text) >= 0 && text != IntPtr.Zero)
            {
                try
                {
                    tooltip = Marshal.PtrToStringBSTR(text);
                }
                finally
                {
                    Marshal.FreeBSTR(text);
                }
            }
            LanguageBarIcon image = null;
            if (_button != null)
            {
                int iconHr = _button.GetIcon(out IntPtr icon);
                try
                {
                    if (iconHr >= 0 && icon != IntPtr.Zero)
                    {
                        _item.GetInfo(out TfLangBarItemInfo info);
                        image = IconPixels.Read(icon, (info.Style & 0x20) != 0);
                    }
                }
                finally
                {
                    if (icon != IntPtr.Zero)
                        GdiNative.DestroyIcon(icon);
                }
            }
            return new InputModeView(image, enabled: enabled, tooltip: tooltip);
        }

        internal bool Click() => _button != null && _button.OnClick(2, 0, 0, IntPtr.Zero) >= 0;

        InputModeView IInputModeSource.Read() => Read();

        void IInputModeSource.Click(Action complete)
        {
            try
            {
                Click();
            }
            finally
            {
                complete();
            }
        }

        public void Dispose()
        {
            Release(_item);
            Release(_thread);
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.ReleaseComObject(value);
        }
    }
}
