using System;
using System.Runtime.InteropServices;
using System.Text;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>
    /// The C ABI of the native library: IngameIME itself plus the thin C layer in
    /// native/shim. See ADR 0023.
    /// </summary>
    internal static class ImeShim
    {
        internal const string LibraryName = "IngameIME.dll";

        [StructLayout(LayoutKind.Sequential)]
        internal struct ShellModeItem
        {
            internal IntPtr Tooltip;
            internal IntPtr Icon;
            internal int Disabled;
            internal int Hidden;
            internal IntPtr Glyph;
            internal IntPtr Reserved;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr iime_shell_mode_open(
            IntPtr host,
            ProfileChangedCallback changed,
            IntPtr user,
            out int result
        );

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void iime_shell_mode_close(IntPtr button);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int iime_shell_mode_read(IntPtr button, out ShellModeItem item);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void iime_shell_mode_free(ref ShellModeItem item);

        internal delegate void PreEditCallback(
            int state,
            int selectionStart,
            int selectionEnd,
            IntPtr content,
            IntPtr user
        );

        internal delegate void CommitCallback(IntPtr text, IntPtr user);

        internal delegate void CandidatesCallback(int state, int selection, IntPtr items, int count, IntPtr user);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ProfileChangedCallback(IntPtr user);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr iime_watch_profiles(ProfileChangedCallback changed, IntPtr user, out int result);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void iime_unwatch_profiles(IntPtr watch);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        internal static extern int iime_get_keyboard_layout_name(IntPtr layout, StringBuilder name, int capacity);

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
