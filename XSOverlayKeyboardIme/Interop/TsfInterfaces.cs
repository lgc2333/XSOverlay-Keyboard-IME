using System;
using System.Runtime.InteropServices;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>
    /// The one TSF interface the plugin consumes, read for the IME name. The plugin
    /// implements none of them: the input context belongs to IngameIME. Only the leading
    /// methods of the vtable are declared, which is all a cast from a COM pointer needs.
    /// See ADR 0009.
    /// </summary>
    [ComImport, Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfInputProcessorProfiles
    {
        [PreserveSig]
        int Register(ref Guid clsid);

        [PreserveSig]
        int Unregister(ref Guid clsid);

        [PreserveSig]
        int AddLanguageProfile(
            ref Guid clsid,
            ushort langid,
            ref Guid profile,
            [MarshalAs(UnmanagedType.LPWStr)] string description,
            uint descriptionLength,
            [MarshalAs(UnmanagedType.LPWStr)] string iconFile,
            uint iconFileLength,
            uint iconIndex
        );

        [PreserveSig]
        int RemoveLanguageProfile(ref Guid clsid, ushort langid, ref Guid profile);

        [PreserveSig]
        int EnumInputProcessorInfo(out IntPtr enumerator);

        [PreserveSig]
        int GetDefaultLanguageProfile(ushort langid, ref Guid category, out Guid clsid, out Guid profile);

        [PreserveSig]
        int SetDefaultLanguageProfile(ref Guid clsid, ushort langid, ref Guid profile);

        [PreserveSig]
        int ActivateLanguageProfile(ref Guid clsid, ushort langid, ref Guid profile);

        [PreserveSig]
        int GetActiveLanguageProfile(ref Guid clsid, out ushort langid, out Guid profile);

        [PreserveSig]
        int GetLanguageProfileDescription(ref Guid clsid, ushort langid, ref Guid profile, out IntPtr description);

        [PreserveSig]
        int GetCurrentLanguage(out ushort langid);
    }
}
