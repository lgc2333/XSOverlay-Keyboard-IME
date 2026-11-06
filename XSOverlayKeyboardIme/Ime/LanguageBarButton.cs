using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The button an input method adds to the TSF language bar, which is the one the
    /// Windows input indicator draws. Reading it yields the icon to mirror; clicking it
    /// asks the IME to switch, so the plugin never writes a mode itself. See ADR 0028.
    /// </summary>
    internal sealed class LanguageBarButton : IDisposable
    {
        /// <summary>TF_LBI_CLK_LEFT, the click the language bar sends for a plain press.</summary>
        private const int ClickLeft = 2;

        /// <summary>Enough for the items a text service adds; a runaway list stops here.</summary>
        private const int MaxItems = 32;

        private readonly ITfThreadMgr _threadManager;
        private readonly ITfLangBarItemButton _button;
        private bool _disposed;

        private LanguageBarButton(ITfThreadMgr threadManager, ITfLangBarItemButton button)
        {
            _threadManager = threadManager;
            _button = button;
        }

        /// <summary>
        /// Looks the button up on the calling thread, which must be the one that owns the
        /// input context. Null when the IME offers no such button.
        /// </summary>
        internal static LanguageBarButton Open()
        {
            IntPtr raw;
            int hr = Win32Native.TF_GetThreadMgr(out raw);
            if (hr < 0 || raw == IntPtr.Zero)
            {
                HostLog.Warn("no TSF thread manager, hr=0x" + hr.ToString("x") + "; the switch has nothing to press");
                return null;
            }

            ITfThreadMgr threadManager;
            try
            {
                threadManager = Marshal.GetObjectForIUnknown(raw) as ITfThreadMgr;
            }
            finally
            {
                Marshal.Release(raw);
            }

            var manager = threadManager as ITfLangBarItemMgr;
            IEnumTfLangBarItems items = null;
            if (manager == null || manager.EnumItems(out items) < 0 || items == null)
            {
                HostLog.Info("the language bar lists nothing yet; the switch waits for it");
                Release(threadManager);
                return null;
            }

            ITfLangBarItemButton found;
            try
            {
                found = PickButton(items);
            }
            finally
            {
                Release(items);
            }

            if (found == null)
            {
                HostLog.Info("no language bar item draws an icon yet; the switch waits for one");
                Release(threadManager);
                return null;
            }

            return new LanguageBarButton(threadManager, found);
        }

        /// <summary>Presses the button, which is the IME's own way to switch 中 and 英.</summary>
        internal bool Click()
        {
            if (_disposed)
            {
                return false;
            }

            int hr = _button.OnClick(ClickLeft, 0, 0, IntPtr.Zero);
            HostLog.Info("language bar button pressed, hr=0x" + hr.ToString("x"));
            return hr >= 0;
        }

        /// <summary>The icon the input indicator would draw, or null when it is unreadable.</summary>
        internal LanguageBarIcon ReadIcon()
        {
            if (_disposed)
            {
                return null;
            }

            IntPtr icon;
            if (_button.GetIcon(out icon) < 0 || icon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return ToIcon(icon);
            }
            finally
            {
                // The item creates the icon for this call, so the caller releases it.
                GdiNative.DestroyIcon(icon);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Release(_button);
            Release(_threadManager);
        }

        /// <summary>
        /// Takes the first item that draws an icon, because the input-mode button is the
        /// one the input indicator can show. The items that draw nothing are other menus.
        /// </summary>
        private static ITfLangBarItemButton PickButton(IEnumTfLangBarItems items)
        {
            ITfLangBarItemButton first = null;
            for (int index = 0; index < MaxItems; index++)
            {
                ITfLangBarItem item;
                uint fetched;
                if (items.Next(1, out item, out fetched) < 0 || fetched == 0 || item == null)
                {
                    return first;
                }

                var button = item as ITfLangBarItemButton;
                bool draws = button != null && Draws(button);
                HostLog.Info("language bar item " + index + (draws ? " draws an icon" : " draws nothing"));

                if (button != null && draws)
                {
                    // The item is the button, so it stays alive for the caller.
                    Release(first);
                    return button;
                }

                if (button != null && first == null)
                {
                    first = button;
                }
                else
                {
                    Release(item);
                }
            }

            return first;
        }

        private static bool Draws(ITfLangBarItemButton button)
        {
            IntPtr icon;
            if (button.GetIcon(out icon) < 0 || icon == IntPtr.Zero)
            {
                return false;
            }

            GdiNative.DestroyIcon(icon);
            return true;
        }

        /// <summary>Copies an icon into straight RGBA, which is what a canvas wants.</summary>
        private static LanguageBarIcon ToIcon(IntPtr icon)
        {
            GdiNative.IconInfo info;
            if (!GdiNative.GetIconInfo(icon, out info))
            {
                return null;
            }

            try
            {
                if (info.Color == IntPtr.Zero)
                {
                    return null;
                }

                GdiNative.Bitmap bitmap;
                if (GdiNative.GetObject(info.Color, GdiNative.BitmapSize, out bitmap) == 0)
                {
                    return null;
                }

                int width = bitmap.Width;
                int height = bitmap.Height;
                if (width <= 0 || height <= 0 || width > 256 || height > 256)
                {
                    return null;
                }

                var header = new GdiNative.BitmapInfo();
                header.Header.Size = (uint)Marshal.SizeOf<GdiNative.BitmapInfoHeader>();
                header.Header.Width = width;
                // A negative height asks for the rows top down, as a canvas wants them.
                header.Header.Height = -height;
                header.Header.Planes = 1;
                header.Header.BitCount = GdiNative.BitCount32;

                var pixels = new byte[width * height * 4];
                IntPtr dc = GdiNative.CreateCompatibleDC(IntPtr.Zero);
                try
                {
                    if (
                        GdiNative.GetDIBits(dc, info.Color, 0, (uint)height, pixels, ref header, GdiNative.DipRgbColors)
                        == 0
                    )
                    {
                        return null;
                    }
                }
                finally
                {
                    GdiNative.DeleteDC(dc);
                }

                return LanguageBarIcon.FromBgra(width, height, pixels);
            }
            finally
            {
                DeleteBitmap(info.Color);
                DeleteBitmap(info.Mask);
            }
        }

        private static void DeleteBitmap(IntPtr bitmap)
        {
            if (bitmap != IntPtr.Zero)
            {
                GdiNative.DeleteObject(bitmap);
            }
        }

        private static void Release(object instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }
    }
}
