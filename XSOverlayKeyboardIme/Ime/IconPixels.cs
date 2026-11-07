using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    internal static class IconPixels
    {
        /// <summary>Copies an icon into straight RGBA, which is what a canvas wants.</summary>
        internal static LanguageBarIcon Read(IntPtr icon, bool tint = false)
        {
            GdiNative.IconInfo info;
            if (!GdiNative.GetIconInfo(icon, out info))
            {
                return null;
            }

            try
            {
                bool monochrome = info.Color == IntPtr.Zero;
                GdiNative.Bitmap bitmap;
                if (GdiNative.GetObject(monochrome ? info.Mask : info.Color, GdiNative.BitmapSize, out bitmap) == 0)
                {
                    return null;
                }

                int width = bitmap.Width;
                int height = bitmap.Height;
                if (width <= 0 || height <= 0 || width > 256 || height > (monochrome ? 512 : 256))
                {
                    return null;
                }

                byte[] pixels = ReadBgra(monochrome ? info.Mask : info.Color, width, height);
                if (pixels == null)
                    return null;

                if (monochrome)
                    return height % 2 == 0 ? LanguageBarIcon.FromMonochrome(width, height / 2, pixels) : null;
                byte[] mask = null;
                if (info.Mask != IntPtr.Zero)
                {
                    mask = ReadBgra(info.Mask, width, height);
                    if (mask == null)
                        return null;
                }
                return LanguageBarIcon.FromBgra(width, height, pixels, mask, tint, premultiplied: true);
            }
            finally
            {
                DeleteBitmap(info.Color);
                DeleteBitmap(info.Mask);
            }
        }

        private static byte[] ReadBgra(IntPtr bitmap, int width, int height)
        {
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
                return
                    GdiNative.GetDIBits(dc, bitmap, 0, (uint)height, pixels, ref header, GdiNative.DipRgbColors)
                    == height
                    ? pixels
                    : null;
            }
            finally
            {
                GdiNative.DeleteDC(dc);
            }
        }

        private static void DeleteBitmap(IntPtr bitmap)
        {
            if (bitmap != IntPtr.Zero)
            {
                GdiNative.DeleteObject(bitmap);
            }
        }
    }
}
