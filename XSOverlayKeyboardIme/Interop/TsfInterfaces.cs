using System;
using System.Runtime.InteropServices;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>TSF interfaces the host consumes. Method order must match the native vtable.</summary>
    [ComImport, Guid("AA80E801-2021-11D2-93E0-0060B067B86E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfThreadMgr
    {
        [PreserveSig]
        int Activate(out int clientId);

        [PreserveSig]
        int Deactivate();

        [PreserveSig]
        int CreateDocumentMgr(out IntPtr documentMgr);

        [PreserveSig]
        int EnumDocumentMgrs(out IntPtr enumerator);

        [PreserveSig]
        int GetFocus(out IntPtr documentMgr);

        [PreserveSig]
        int SetFocus(IntPtr documentMgr);
    }

    [ComImport, Guid("4EA48A35-60AE-446F-8FD6-E6A8D82459F7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfSource
    {
        [PreserveSig]
        int AdviseSink(ref Guid riid, IntPtr punk, out uint cookie);

        [PreserveSig]
        int UnadviseSink(uint cookie);
    }

    [ComImport, Guid("EA1EA135-19DF-11D7-A6D2-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfUIElementMgr
    {
        [PreserveSig]
        int BeginUIElement(IntPtr element, out int show, out uint id);

        [PreserveSig]
        int UpdateUIElement(uint id);

        [PreserveSig]
        int EndUIElement(uint id);

        [PreserveSig]
        int GetUIElement(uint id, out ITfUIElement element);

        [PreserveSig]
        int EnumUIElements(out IntPtr enumerator);
    }

    [ComImport, Guid("EA1EA137-19DF-11D7-A6D2-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfUIElement
    {
        [PreserveSig]
        int GetDescription(out IntPtr description);

        [PreserveSig]
        int GetGUID(out Guid guid);

        [PreserveSig]
        int Show(int show);

        [PreserveSig]
        int IsShown(out int shown);
    }

    [ComImport, Guid("EA1EA138-19DF-11D7-A6D2-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfCandidateListUIElement
    {
        [PreserveSig]
        int GetDescription(out IntPtr description);

        [PreserveSig]
        int GetGUID(out Guid guid);

        [PreserveSig]
        int Show(int show);

        [PreserveSig]
        int IsShown(out int shown);

        [PreserveSig]
        int GetUpdatedFlags(out uint flags);

        [PreserveSig]
        int GetDocumentMgr(out IntPtr documentMgr);

        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetSelection(out uint selection);

        [PreserveSig]
        int GetString(uint index, out IntPtr text);

        [PreserveSig]
        int GetPageIndex([Out] uint[] indexes, uint size, out uint pageCount);

        [PreserveSig]
        int SetPageIndex([In] uint[] indexes, uint size);

        [PreserveSig]
        int GetCurrentPage(out uint page);
    }

    [ComImport, Guid("7DCF57AC-18AD-438B-824D-979BFFB74B7C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfCompartmentMgr
    {
        [PreserveSig]
        int GetCompartment(ref Guid guid, out ITfCompartment compartment);

        [PreserveSig]
        int ClearCompartment(int clientId, ref Guid guid);

        [PreserveSig]
        int EnumCompartments(out IntPtr enumerator);
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

    /// <summary>The pre-8.1 input processor enumeration. See ADR 0009.</summary>
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

    /// <summary>Implemented by managed code; the native side calls back through the classic CCW.</summary>
    [ComImport, Guid("EA1EA136-19DF-11D7-A6D2-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfUIElementSink
    {
        [PreserveSig]
        int BeginUIElement(uint id, [MarshalAs(UnmanagedType.Bool)] out bool show);

        [PreserveSig]
        int UpdateUIElement(uint id);

        [PreserveSig]
        int EndUIElement(uint id);
    }
}
