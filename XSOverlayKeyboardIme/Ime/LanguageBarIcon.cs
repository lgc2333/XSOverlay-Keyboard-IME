using System;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The language bar icon as pixels the page can draw, with a key that changes only
    /// when the picture does. See ADR 0028.
    /// </summary>
    internal sealed class LanguageBarIcon
    {
        private LanguageBarIcon(int width, int height, byte[] rgba, bool tint)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
            Tint = tint;
            Key = Fingerprint(width, height, rgba) + (tint ? ":tint" : string.Empty);
        }

        internal int Width { get; }

        internal int Height { get; }

        /// <summary>Straight RGBA, row by row, top down.</summary>
        internal byte[] Rgba { get; }
        internal bool Tint { get; }

        /// <summary>Fingerprint of the picture, for telling one icon from the next.</summary>
        internal string Key { get; }

        /// <summary>Turns the 32-bit DIB order into RGBA, and makes a key from the result.</summary>
        internal static LanguageBarIcon FromBgra(
            int width,
            int height,
            byte[] pixels,
            byte[] mask = null,
            bool tint = false,
            bool premultiplied = false
        )
        {
            var rgba = new byte[pixels.Length];
            bool opaque = false;
            for (int index = 0; index < pixels.Length; index += 4)
            {
                byte blue = pixels[index];
                byte green = pixels[index + 1];
                byte red = pixels[index + 2];
                byte alpha = pixels[index + 3];
                rgba[index] = Straight(red, alpha, premultiplied);
                rgba[index + 1] = Straight(green, alpha, premultiplied);
                rgba[index + 2] = Straight(blue, alpha, premultiplied);
                rgba[index + 3] = alpha;
                opaque |= alpha != 0;
            }

            // A mask-based icon carries no alpha; drawing it transparent would hide it.
            if (!opaque)
            {
                for (int index = 3; index < rgba.Length; index += 4)
                {
                    rgba[index] = mask != null && mask[index - 3] != 0 ? (byte)0 : (byte)255;
                }
            }

            return new LanguageBarIcon(width, height, rgba, tint);
        }

        internal static LanguageBarIcon FromMonochrome(int width, int height, byte[] andXor)
        {
            int length = width * height * 4;
            var rgba = new byte[length];
            for (int index = 0; index < length; index += 4)
            {
                rgba[index] = rgba[index + 1] = rgba[index + 2] = 255;
                // Transparent AND-only pixels stay clear; ink and inversion follow the theme.
                rgba[index + 3] = andXor[index] != 0 && andXor[length + index] == 0 ? (byte)0 : (byte)255;
            }
            return new LanguageBarIcon(width, height, rgba, true);
        }

        private static byte Straight(byte color, byte alpha, bool premultiplied) =>
            premultiplied && alpha > 0 ? (byte)Math.Min(255, (color * 255 + alpha / 2) / alpha) : color;

        private static string Fingerprint(int width, int height, byte[] pixels)
        {
            uint hash = 2166136261;
            hash = (hash ^ (uint)width) * 16777619;
            hash = (hash ^ (uint)height) * 16777619;
            for (int index = 0; index < pixels.Length; index++)
            {
                hash = (hash ^ pixels[index]) * 16777619;
            }

            return width + "x" + height + ":" + hash.ToString("x8");
        }
    }
}
