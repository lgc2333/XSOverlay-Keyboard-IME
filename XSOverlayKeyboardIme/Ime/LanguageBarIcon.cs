using System;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The language bar icon as pixels the page can draw, with a key that changes only
    /// when the picture does. See ADR 0028.
    /// </summary>
    internal sealed class LanguageBarIcon
    {
        private LanguageBarIcon(int width, int height, byte[] rgba, string key)
        {
            Width = width;
            Height = height;
            Rgba = rgba;
            Key = key;
        }

        internal int Width { get; }

        internal int Height { get; }

        /// <summary>Straight RGBA, row by row, top down.</summary>
        internal byte[] Rgba { get; }

        /// <summary>Fingerprint of the picture, for telling one icon from the next.</summary>
        internal string Key { get; }

        /// <summary>Turns the 32-bit DIB order into RGBA, and makes a key from the result.</summary>
        internal static LanguageBarIcon FromBgra(int width, int height, byte[] pixels)
        {
            var rgba = new byte[pixels.Length];
            bool opaque = false;
            for (int index = 0; index < pixels.Length; index += 4)
            {
                byte blue = pixels[index];
                byte green = pixels[index + 1];
                byte red = pixels[index + 2];
                byte alpha = pixels[index + 3];
                rgba[index] = red;
                rgba[index + 1] = green;
                rgba[index + 2] = blue;
                rgba[index + 3] = alpha;
                opaque |= alpha != 0;
            }

            // A mask-based icon carries no alpha; drawing it transparent would hide it.
            if (!opaque)
            {
                for (int index = 3; index < rgba.Length; index += 4)
                {
                    rgba[index] = 255;
                }
            }

            return new LanguageBarIcon(width, height, rgba, Fingerprint(width, height, rgba));
        }

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
