using System;
using System.Runtime.InteropServices;

namespace XSOverlayKeyboardIme.Interop
{
    /// <summary>
    /// The TSF interfaces the plugin consumes. The input context and notification
    /// sink remain native. See ADR 0022, ADR 0028 and ADR 0045.
    ///
    /// Every method stays in header order, including the ones the plugin never calls: a
    /// COM vtable is positional, so dropping one would shift every call after it.
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

        [PreserveSig]
        int ChangeCurrentLanguage(ushort langid);

        [PreserveSig]
        int GetLanguageList(out IntPtr languages, out uint count);

        [PreserveSig]
        int EnumLanguageProfiles(ushort langid, out IntPtr enumerator);
    }

    /// <summary>TF_INPUTPROCESSORPROFILE, in Windows SDK field order.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct TfInputProcessorProfile
    {
        internal uint Type;
        internal ushort Language;
        internal Guid Service;
        internal Guid Profile;
        internal Guid Category;
        internal IntPtr Substitute;
        internal uint Capabilities;
        internal IntPtr Layout;
        internal uint Flags;
    }

    [ComImport, Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfInputProcessorProfileMgr
    {
        [PreserveSig]
        int ActivateProfile(uint type, ushort language, ref Guid service, ref Guid profile, IntPtr layout, uint flags);

        [PreserveSig]
        int DeactivateProfile(
            uint type,
            ushort language,
            ref Guid service,
            ref Guid profile,
            IntPtr layout,
            uint flags
        );

        [PreserveSig]
        int GetProfile(
            uint type,
            ushort language,
            ref Guid service,
            ref Guid profile,
            IntPtr layout,
            out TfInputProcessorProfile value
        );

        [PreserveSig]
        int EnumProfiles(ushort language, out IEnumTfInputProcessorProfiles enumerator);

        [PreserveSig]
        int ReleaseInputProcessor(ref Guid service, uint flags);

        [PreserveSig]
        int RegisterProfile(
            ref Guid service,
            ushort language,
            ref Guid profile,
            [MarshalAs(UnmanagedType.LPWStr)] string description,
            uint descriptionLength,
            [MarshalAs(UnmanagedType.LPWStr)] string iconFile,
            uint iconFileLength,
            uint iconIndex,
            IntPtr substitute,
            uint preferredLayout,
            int enabledByDefault,
            uint flags
        );

        [PreserveSig]
        int UnregisterProfile(ref Guid service, ushort language, ref Guid profile, uint flags);

        [PreserveSig]
        int GetActiveProfile(ref Guid category, out TfInputProcessorProfile profile);
    }

    [ComImport, Guid("71C6E74D-0F28-11D8-A82A-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumTfInputProcessorProfiles
    {
        [PreserveSig]
        int Clone(out IEnumTfInputProcessorProfiles enumerator);

        [PreserveSig]
        int Next(
            uint count,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] TfInputProcessorProfile[] profiles,
            out uint fetched
        );

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Skip(uint count);
    }

    /// <summary>
    /// The thread manager TSF hands to whoever owns the thread. The plugin reads the
    /// language bar item out of it and never activates a client of its own.
    /// </summary>
    [ComImport, Guid("AA80E801-2021-11D2-93E0-0060B067B86E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfThreadMgr
    {
        [PreserveSig]
        int Activate(out int clientId);

        [PreserveSig]
        int Deactivate();

        [PreserveSig]
        int CreateDocumentMgr(out IntPtr documentManager);

        [PreserveSig]
        int EnumDocumentMgrs(out IntPtr enumerator);

        [PreserveSig]
        int GetFocus(out IntPtr documentManager);

        [PreserveSig]
        int SetFocus(IntPtr documentManager);

        [PreserveSig]
        int AssociateFocus(IntPtr window, IntPtr documentManager, out IntPtr previous);

        [PreserveSig]
        int IsThreadFocus(out int focused);
    }

    /// <summary>One language bar item as the input indicator would draw it. See ADR 0028.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct TfLangBarItemInfo
    {
        internal Guid Service;
        internal Guid Item;
        internal uint Style;
        internal uint Sort;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string Description;
    }

    /// <summary>The item a text service adds to the language bar. See ADR 0028.</summary>
    [ComImport, Guid("73540D69-EDEB-4EE9-96C9-23AA30B25916"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfLangBarItem
    {
        [PreserveSig]
        int GetInfo(out TfLangBarItemInfo info);

        [PreserveSig]
        int GetStatus(out uint status);

        [PreserveSig]
        int Show(int show);

        [PreserveSig]
        int GetTooltipString(out IntPtr tooltip);
    }

    /// <summary>Declares the inherited methods first, because a COM vtable is flat.</summary>
    [ComImport, Guid("28C7F1D0-DE25-11D2-AFDD-00105A2799B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfLangBarItemButton
    {
        [PreserveSig]
        int GetInfo(out TfLangBarItemInfo info);

        [PreserveSig]
        int GetStatus(out uint status);

        [PreserveSig]
        int Show(int show);

        [PreserveSig]
        int GetTooltipString(out IntPtr tooltip);

        [PreserveSig]
        int OnClick(int click, int x, int y, IntPtr area);

        [PreserveSig]
        int InitMenu(IntPtr menu);

        [PreserveSig]
        int OnMenuSelect(uint id);

        [PreserveSig]
        int GetIcon(out IntPtr icon);

        [PreserveSig]
        int GetText(out IntPtr text);
    }

    [ComImport, Guid("583F34D0-DE25-11D2-AFDD-00105A2799B5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEnumTfLangBarItems
    {
        [PreserveSig]
        int Clone(out IEnumTfLangBarItems enumerator);

        [PreserveSig]
        int Next(uint count, out ITfLangBarItem item, out uint fetched);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Skip(uint count);
    }

    [ComImport, Guid("BA468C55-9956-4FB1-A59D-52A7DD7CC6AA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITfLangBarItemMgr
    {
        [PreserveSig]
        int EnumItems(out IEnumTfLangBarItems enumerator);

        [PreserveSig]
        int GetItem(ref Guid item, out ITfLangBarItem result);

        [PreserveSig]
        int AddItem(ITfLangBarItem item);

        [PreserveSig]
        int RemoveItem(ITfLangBarItem item);

        [PreserveSig]
        int AdviseItemSink(IntPtr sink, out uint cookie, ref Guid item);

        [PreserveSig]
        int UnadviseItemSink(uint cookie);

        [PreserveSig]
        int GetItemFloatingRect(uint thread, ref Guid item, out IntPtr rect);

        [PreserveSig]
        int GetItemsStatus(uint count, IntPtr items, IntPtr status);

        [PreserveSig]
        int GetItemNum(out uint count);
    }
}
