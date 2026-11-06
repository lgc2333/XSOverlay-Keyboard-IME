using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The keyboard profiles TSF keeps for this thread: the name of the profile this
    /// thread uses, and the way to move the thread onto another language's default
    /// profile. See ADR 0009 and ADR 0030.
    /// </summary>
    internal sealed class ImeProfiles : IDisposable
    {
        private static readonly Guid InputProcessorProfiles = new Guid("33C53A50-F456-4884-B049-85FD643ECFED");
        private static readonly Guid KeyboardCategory = new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");

        private readonly ITfInputProcessorProfiles _profiles;
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
        /// The description of this thread's default profile, or the language's own name when
        /// the language has no text service to describe, as a legacy layout does.
        /// </summary>
        internal string ReadName()
        {
            ushort language;
            if (_disposed || _profiles == null || _profiles.GetCurrentLanguage(out language) < 0)
            {
                return string.Empty;
            }

            string description = Describe(language);
            return description.Length > 0 ? description : LanguageName(language);
        }

        private string Describe(ushort language)
        {
            Guid clsid;
            Guid profile;
            if (!TryDefaultProfile(language, out clsid, out profile))
            {
                return string.Empty;
            }

            IntPtr description;
            if (
                _profiles.GetLanguageProfileDescription(ref clsid, language, ref profile, out description) < 0
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

        private static string LanguageName(ushort language)
        {
            var buffer = new System.Text.StringBuilder(64);
            int length = Win32Native.GetLocaleInfoW(language, Win32Native.LocaleDisplayName, buffer, buffer.Capacity);
            return length <= 1 ? string.Empty : buffer.ToString(0, length - 1);
        }

        /// <summary>Moves this thread onto the language's default keyboard profile.</summary>
        internal void Activate(ushort language)
        {
            Guid clsid;
            Guid profile;
            if (!TryDefaultProfile(language, out clsid, out profile))
            {
                return;
            }

            int hr = _profiles.ActivateLanguageProfile(ref clsid, language, ref profile);
            if (hr < 0)
            {
                HostLog.Warn("the language profile did not activate, hr=0x" + hr.ToString("x"));
            }
        }

        /// <summary>The default keyboard profile of a language, or false when unreadable.</summary>
        private bool TryDefaultProfile(ushort language, out Guid clsid, out Guid profile)
        {
            clsid = Guid.Empty;
            profile = Guid.Empty;
            if (_disposed || _profiles == null)
            {
                return false;
            }

            Guid category = KeyboardCategory;
            int hr = _profiles.GetDefaultLanguageProfile(language, ref category, out clsid, out profile);
            if (hr >= 0)
            {
                return true;
            }

            HostLog.Warn("no default profile for language 0x" + language.ToString("x4") + ", hr=0x" + hr.ToString("x"));
            return false;
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
