using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Enabled keyboard inputs and this thread's real active input. See ADR 0044 and ADR 0045.</summary>
    internal sealed class ImeProfiles : IDisposable
    {
        private const int ProfileBatch = 8;
        private const uint ProfileEnabled = 2;
        private const uint ActivateForProcess = 0x10000000;

        private static readonly Guid InputProcessorProfiles = new Guid("33C53A50-F456-4884-B049-85FD643ECFED");
        private static readonly Guid KeyboardCategory = new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");

        private readonly ITfInputProcessorProfileMgr _manager;
        private readonly ITfInputProcessorProfiles _profiles;
        private readonly List<ImeChoice> _choices = new List<ImeChoice>();
        private bool _disposed;

        private ImeProfiles(ITfInputProcessorProfileMgr manager, ITfInputProcessorProfiles profiles)
        {
            _manager = manager;
            _profiles = profiles;
        }

        internal static ImeProfiles Open()
        {
            Guid clsid = InputProcessorProfiles;
            Guid iid = typeof(ITfInputProcessorProfileMgr).GUID;
            int hr = Win32Native.CoCreateInstance(
                ref clsid,
                IntPtr.Zero,
                Win32Native.ClsCtxInProcServer,
                ref iid,
                out IntPtr unknown
            );
            if (hr < 0 || unknown == IntPtr.Zero)
            {
                HostLog.Warn("the keyboard inputs are unavailable, hr=0x" + hr.ToString("x"));
                return null;
            }

            try
            {
                object instance = Marshal.GetObjectForIUnknown(unknown);
                var manager = instance as ITfInputProcessorProfileMgr;
                var profiles = instance as ITfInputProcessorProfiles;
                if (manager == null || profiles == null)
                {
                    Release(instance);
                    return null;
                }

                return new ImeProfiles(manager, profiles);
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        /// <summary>No default-profile fallback: an unreadable active input stays unknown.</summary>
        internal LanguageProfile? ReadActive()
        {
            if (_disposed)
                return null;
            Guid category = KeyboardCategory;
            int hr = _manager.GetActiveProfile(ref category, out TfInputProcessorProfile active);
            if (hr != 0 || !IsKeyboard(active))
            {
                HostLog.Warn("the active keyboard input is unavailable, hr=0x" + hr.ToString("x"));
                return null;
            }

            return Identity(active);
        }

        internal string Describe(LanguageProfile? profile)
        {
            if (_disposed || profile == null)
                return string.Empty;
            LanguageProfile input = profile.Value;
            if (input.Type == KeyboardInputType.KeyboardLayout)
            {
                var buffer = new StringBuilder(256);
                return ImeShim.iime_get_keyboard_layout_name(input.Layout, buffer, buffer.Capacity) > 0
                    ? buffer.ToString()
                    : string.Empty;
            }

            Guid service = input.Service;
            Guid id = input.Profile;
            int hr = _profiles.GetLanguageProfileDescription(ref service, input.Language, ref id, out IntPtr name);
            if (hr < 0 || name == IntPtr.Zero)
                return string.Empty;
            try
            {
                return Marshal.PtrToStringBSTR(name) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeBSTR(name);
            }
        }

        internal IReadOnlyList<ImeChoice> ListChoices()
        {
            _choices.Clear();
            if (_disposed)
                return _choices;
            bool hasCurrent = _profiles.GetCurrentLanguage(out ushort current) >= 0;
            if (hasCurrent)
                ReadLanguageProfiles(current);
            foreach (ushort language in ReadLanguages())
            {
                if (!hasCurrent || language != current)
                    ReadLanguageProfiles(language);
            }

            return _choices;
        }

        private ushort[] ReadLanguages()
        {
            int hr = _profiles.GetLanguageList(out IntPtr list, out uint count);
            if (hr < 0 || list == IntPtr.Zero)
            {
                HostLog.Warn("the language list could not be read");
                return Array.Empty<ushort>();
            }

            try
            {
                var languages = new ushort[count];
                for (uint index = 0; index < count; index++)
                    languages[index] = unchecked((ushort)Marshal.ReadInt16(list, (int)index * sizeof(ushort)));
                return languages;
            }
            finally
            {
                Marshal.FreeCoTaskMem(list);
            }
        }

        private void ReadLanguageProfiles(ushort language)
        {
            int hr = _manager.EnumProfiles(language, out IEnumTfInputProcessorProfiles items);
            if (hr < 0 || items == null)
            {
                HostLog.Warn("the keyboard inputs for language 0x" + language.ToString("x") + " are unavailable");
                return;
            }

            try
            {
                string languageName = LanguageName(language);
                var batch = new TfInputProcessorProfile[ProfileBatch];
                while (items.Next((uint)batch.Length, batch, out uint fetched) >= 0 && fetched > 0)
                {
                    for (int index = 0; index < fetched; index++)
                    {
                        TfInputProcessorProfile row = batch[index];
                        if ((row.Flags & ProfileEnabled) == 0 || !IsKeyboard(row))
                            continue;
                        LanguageProfile profile = Identity(row);
                        string name = Describe(profile);
                        _choices.Add(new ImeChoice(name.Length == 0 ? "未知输入法" : name, languageName, profile));
                    }
                }
            }
            finally
            {
                Release(items);
            }
        }

        internal bool TryGetChoice(int index, out LanguageProfile profile)
        {
            if (!_disposed && index >= 0 && index < _choices.Count)
            {
                profile = _choices[index].Profile;
                return true;
            }

            profile = default;
            return false;
        }

        /// <summary>The caller must first acquire foreground. See ADR 0045.</summary>
        internal bool Activate(LanguageProfile profile)
        {
            if (_disposed)
                return false;
            int hr = _profiles.GetCurrentLanguage(out ushort current);
            if (hr < 0)
                return false;
            if (current != profile.Language)
            {
                hr = _profiles.ChangeCurrentLanguage(profile.Language);
                if (hr != 0)
                {
                    HostLog.Warn("the input language could not be changed, hr=0x" + hr.ToString("x"));
                    return false;
                }
            }

            Guid service = profile.Service;
            Guid id = profile.Profile;
            hr = _manager.ActivateProfile(
                (uint)profile.Type,
                profile.Language,
                ref service,
                ref id,
                profile.Layout,
                ActivateForProcess
            );
            if (hr != 0)
                HostLog.Warn("the keyboard input could not be activated, hr=0x" + hr.ToString("x"));
            return hr == 0;
        }

        private static bool IsKeyboard(TfInputProcessorProfile profile)
        {
            return profile.Type == (uint)KeyboardInputType.KeyboardLayout
                || (profile.Type == (uint)KeyboardInputType.TextService && profile.Category == KeyboardCategory);
        }

        private static LanguageProfile Identity(TfInputProcessorProfile profile)
        {
            return new LanguageProfile(
                (KeyboardInputType)profile.Type,
                profile.Language,
                profile.Service,
                profile.Profile,
                profile.Layout
            );
        }

        private static string LanguageName(ushort language)
        {
            var buffer = new StringBuilder(64);
            int length = Win32Native.GetLocaleInfoW(language, Win32Native.LocaleDisplayName, buffer, buffer.Capacity);
            return length <= 1 ? string.Empty : buffer.ToString(0, length - 1);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            // Both interfaces belong to the same RCW.
            Release(_manager);
        }

        private static void Release(object instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
                Marshal.ReleaseComObject(instance);
        }
    }
}
