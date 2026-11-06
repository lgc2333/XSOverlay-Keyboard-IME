using System;
using System.Runtime.InteropServices;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>
    /// The TSF interfaces the plugin consumes. It implements none of them: the input
    /// context belongs to IngameIME. Only the leading methods of each vtable are
    /// declared, which is all a cast from a COM pointer needs.
    /// </summary>
    [ComImport, Guid("AA80E801-2021-11D2-93E0-0060B067B86E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfThreadMgr
    {
        [PreserveSig]
        int Activate(out int clientId);

        [PreserveSig]
        int Deactivate();
    }

    [ComImport, Guid("7DCF57AC-18AD-438B-824D-979BFFB74B7C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfCompartmentMgr
    {
        [PreserveSig]
        int GetCompartment(ref Guid guid, out ITfCompartment compartment);
    }

    /// <summary>A VARIANT carrying an integer. Sized for x64, the only platform the game runs on.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct NativeVariant
    {
        internal const ushort TypeInt32 = 3;

        [FieldOffset(0)]
        internal ushort Type;

        [FieldOffset(8)]
        internal int Integer;
    }

    [ComImport, Guid("BB08F7A9-607A-4384-8623-056892B64371"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfCompartment
    {
        [PreserveSig]
        int SetValue(int clientId, ref NativeVariant value);

        [PreserveSig]
        int GetValue(out NativeVariant value);
    }

    /// <summary>The pre-8.1 input processor enumeration, read for the IME name. See ADR 0009.</summary>
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
