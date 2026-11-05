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
