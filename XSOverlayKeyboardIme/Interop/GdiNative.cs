using System;
using System.Runtime.InteropServices;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>GDI entry points for reading an icon into pixels. Nothing here touches the screen.</summary>
    internal static class GdiNative
    {
        internal const ushort BitCount32 = 32;
        internal const uint DipRgbColors = 0;
        internal const int BitmapSize = 32;

        [StructLayout(LayoutKind.Sequential)]
        internal struct IconInfo
        {
            internal int IsIcon;
            internal int HotspotX;
            internal int HotspotY;
            internal IntPtr Mask;
            internal IntPtr Color;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Bitmap
        {
            internal int Type;
            internal int Width;
            internal int Height;
            internal int WidthBytes;
            internal ushort Planes;
            internal ushort BitCount;
            internal IntPtr Bits;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BitmapInfoHeader
        {
            internal uint Size;
            internal int Width;
            internal int Height;
            internal ushort Planes;
            internal ushort BitCount;
            internal uint Compression;
            internal uint SizeImage;
            internal int PelsPerMeterX;
            internal int PelsPerMeterY;
            internal uint ColorsUsed;
            internal uint ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BitmapInfo
        {
            internal BitmapInfoHeader Header;
            internal uint Colors;
        }

        [DllImport("user32.dll")]
        internal static extern bool GetIconInfo(IntPtr icon, out IconInfo info);

        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr icon);

        [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
        internal static extern int GetObject(IntPtr handle, int size, out Bitmap bitmap);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr reference);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteObject(IntPtr handle);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        internal static extern int GetDIBits(
            IntPtr dc,
            IntPtr bitmap,
            uint start,
            uint lines,
            byte[] bits,
            ref BitmapInfo info,
            uint usage
        );
    }
}
