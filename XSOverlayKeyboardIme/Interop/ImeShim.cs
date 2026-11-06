using System;
using System.Runtime.InteropServices;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>
    /// The C ABI of the native library: IngameIME itself plus the thin C layer in
    /// native/shim. See ADR 0023.
    /// </summary>
    internal static class ImeShim
    {
        internal const string LibraryName = "IngameIME.dll";

        internal delegate void PreEditCallback(
            int state,
            int selectionStart,
            int selectionEnd,
            IntPtr content,
            IntPtr user
        );

        internal delegate void CommitCallback(IntPtr text, IntPtr user);

        internal delegate void CandidatesCallback(int state, int selection, IntPtr items, int count, IntPtr user);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr iime_create(IntPtr window);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void iime_destroy(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void iime_set_callbacks(
            IntPtr handle,
            PreEditCallback preedit,
            CommitCallback commit,
            CandidatesCallback candidates,
            IntPtr user
        );

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void iime_set_activated(IntPtr handle, int activated);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void iime_set_preedit_rect(IntPtr handle, int x, int y, int width, int height);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr iime_version();
    }
}
