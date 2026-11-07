using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The keyboard profile TSF keeps for this thread: which IME it composes with, the
    /// name that IME shows, and the list the page may switch to. See ADR 0009, ADR 0035
    /// and ADR 0039.
    /// </summary>
    internal sealed class ImeProfiles : IDisposable
    {
        /// <summary>Rows asked for at a time; a language lists a handful of profiles.</summary>
        private const int ProfileBatch = 8;

        private static readonly Guid InputProcessorProfiles = new Guid("33C53A50-F456-4884-B049-85FD643ECFED");
        private static readonly Guid KeyboardCategory = new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");

        private readonly ITfInputProcessorProfiles _profiles;
        private readonly List<ImeChoice> _choices = new List<ImeChoice>();
        private bool _disposed;

        private ImeProfiles(ITfInputProcessorProfiles profiles)
        {
            _profiles = profiles;
        }

        /// <summary>Null when TSF does not hand out the profiles object.</summary>
        internal static ImeProfiles Open()
        {
            Guid clsid = InputProcessorProfiles;
            Guid iid = typeof(ITfInputProcessorProfiles).GUID;
            IntPtr unknown;
            int hr = Win32Native.CoCreateInstance(
                ref clsid,
                IntPtr.Zero,
                Win32Native.ClsCtxInProcServer,
                ref iid,
                out unknown
            );

            if (hr < 0 || unknown == IntPtr.Zero)
            {
                HostLog.Warn("no input processor profiles, hr=0x" + hr.ToString("x"));
                return null;
            }

            try
            {
                return new ImeProfiles(Marshal.GetObjectForIUnknown(unknown) as ITfInputProcessorProfiles);
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        /// <summary>
        /// The name of the IME this thread composes with: the description of the profile
        /// TSF marks active. Falls back to the language's default profile, then to the
        /// language's own name for a legacy layout with no text service.
        /// </summary>
        internal string ReadName()
        {
            ushort language;
            if (_disposed || _profiles == null || _profiles.GetCurrentLanguage(out language) < 0)
            {
                return string.Empty;
            }

            string active = Describe(ReadActive(language));
            if (active.Length > 0)
            {
                return active;
            }

            string fallback = Describe(ReadDefault(language));
            return fallback.Length > 0 ? fallback : LanguageName(language);
        }

        private string Describe(LanguageProfile? profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            Guid clsid = profile.Value.Service;
            Guid guid = profile.Value.Profile;
            IntPtr description;
            if (
                _profiles.GetLanguageProfileDescription(ref clsid, profile.Value.Language, ref guid, out description)
                    < 0
                || description == IntPtr.Zero
            )
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringBSTR(description) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeBSTR(description);
            }
        }

        /// <summary>
        /// Every IME this machine registers, the current language first, in the order TSF
        /// lists them. See ADR 0039.
        /// </summary>
        internal IReadOnlyList<ImeChoice> ListChoices()
        {
            // A failed read leaves no half list behind. See ADR 0039.
            _choices.Clear();
            if (_disposed || _profiles == null)
            {
                return _choices;
            }

            ushort current;
            bool hasCurrent = _profiles.GetCurrentLanguage(out current) >= 0;
            if (hasCurrent)
            {
                ReadLanguageProfiles(current);
            }

            foreach (ushort language in ReadLanguages())
            {
                if (hasCurrent && language == current)
                {
                    continue;
                }

                ReadLanguageProfiles(language);
            }

            if (_choices.Count == 0)
            {
                HostLog.Warn("no language profile could be read; the dropdown is empty");
            }

            return _choices;
        }

        /// <summary>The languages this machine has installed, as TSF lists them.</summary>
        private ushort[] ReadLanguages()
        {
            IntPtr list;
            uint count;
            if (_profiles.GetLanguageList(out list, out count) < 0 || list == IntPtr.Zero || count == 0)
            {
                HostLog.Warn("the language list could not be read");
                return Array.Empty<ushort>();
            }

            try
            {
                var languages = new ushort[count];
                for (uint index = 0; index < count; index++)
                {
                    languages[index] = unchecked((ushort)Marshal.ReadInt16(list, (int)index * sizeof(ushort)));
                }

                return languages;
            }
            finally
            {
                Marshal.FreeCoTaskMem(list);
            }
        }

        /// <summary>Adds the profiles one language registers, named and tagged with the language.</summary>
        private void ReadLanguageProfiles(ushort language)
        {
            IEnumTfLanguageProfiles items;
            if (_profiles.EnumLanguageProfiles(language, out items) < 0 || items == null)
            {
                HostLog.Warn("the profile list of language 0x" + language.ToString("x") + " could not be read");
                return;
            }

            try
            {
                string languageName = LanguageName(language);
                var batch = new TfLanguageProfile[ProfileBatch];
                uint fetched;
                while (items.Next((uint)batch.Length, batch, out fetched) >= 0 && fetched > 0)
                {
                    for (int index = 0; index < fetched; index++)
                    {
                        TfLanguageProfile row = batch[index];
                        var profile = new LanguageProfile(row.Service, row.Language, row.Profile, row.Active != 0);
                        string name = Describe(profile);
                        if (name.Length > 0)
                        {
                            _choices.Add(new ImeChoice(name, languageName, profile));
                        }
                    }
                }
            }
            finally
            {
                if (Marshal.IsComObject(items))
                {
                    Marshal.ReleaseComObject(items);
                }
            }
        }

        /// <summary>Makes this thread compose with the choice at that index. See ADR 0039.</summary>
        internal bool Activate(int index)
        {
            if (_disposed || _profiles == null || index < 0 || index >= _choices.Count)
            {
                return false;
            }

            LanguageProfile profile = _choices[index].Profile;
            Guid service = profile.Service;
            Guid guid = profile.Profile;
            int hr = _profiles.ActivateLanguageProfile(ref service, profile.Language, ref guid);
            if (hr < 0)
            {
                HostLog.Warn("the profile could not be activated, hr=0x" + hr.ToString("x"));
                return false;
            }

            return true;
        }

        /// <summary>The profile this thread has active, or null when TSF cannot say.</summary>
        private LanguageProfile? ReadActive(ushort language)
        {
            IEnumTfLanguageProfiles items;
            if (_profiles.EnumLanguageProfiles(language, out items) < 0 || items == null)
            {
                return null;
            }

            try
            {
                var listed = new List<LanguageProfile>(ProfileBatch);
                var batch = new TfLanguageProfile[ProfileBatch];
                uint fetched;
                while (items.Next((uint)batch.Length, batch, out fetched) >= 0 && fetched > 0)
                {
                    for (int index = 0; index < fetched; index++)
                    {
                        TfLanguageProfile row = batch[index];
                        listed.Add(new LanguageProfile(row.Service, row.Language, row.Profile, row.Active != 0));
                    }
                }

                return LanguageProfile.TryFindActive(listed, out LanguageProfile active)
                    ? active
                    : (LanguageProfile?)null;
            }
            finally
            {
                if (Marshal.IsComObject(items))
                {
                    Marshal.ReleaseComObject(items);
                }
            }
        }

        /// <summary>The language's default profile, or null when unreadable.</summary>
        private LanguageProfile? ReadDefault(ushort language)
        {
            Guid category = KeyboardCategory;
            Guid clsid;
            Guid profile;
            if (_profiles.GetDefaultLanguageProfile(language, ref category, out clsid, out profile) < 0)
            {
                return null;
            }

            return new LanguageProfile(clsid, language, profile, false);
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
            {
                return;
            }

            _disposed = true;
            if (_profiles != null && Marshal.IsComObject(_profiles))
            {
                Marshal.ReleaseComObject(_profiles);
            }
        }
    }
}
