using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// Loads the native library from the directory the plugin sits in. The plugin
    /// directory is not on the loader's search path and the game's own directory is, so
    /// the load is explicit and a plain import later resolves against the loaded module.
    /// See ADR 0023.
    /// </summary>
    internal static class ImeLibraries
    {
        private const uint LoadWithAlteredSearchPath = 0x00000008;

        internal static bool TryLoad(string directory, out string error)
        {
            string path = Path.Combine(directory, ImeShim.LibraryName);
            if (!File.Exists(path))
            {
                error = path + " is missing";
                return false;
            }

            IntPtr module = LoadLibraryExW(path, IntPtr.Zero, LoadWithAlteredSearchPath);
            if (module == IntPtr.Zero)
            {
                int code = Marshal.GetLastWin32Error();
                error = "loading " + path + " failed (" + code + "): " + new Win32Exception(code).Message;
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>The library's own name, which proves the import path works at all.</summary>
        internal static string Describe()
        {
            try
            {
                return Marshal.PtrToStringAnsi(ImeShim.iime_version()) ?? "(no version)";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);
    }
}
