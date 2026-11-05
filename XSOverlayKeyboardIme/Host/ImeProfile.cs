using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Names the IME, as far as the default profile of the current language reveals it.
    /// The active profile API is unavailable here. See ADR 0009.
    /// </summary>
    internal static class ImeProfile
    {
        private static readonly Guid InputProcessorProfiles = new Guid("33C53A50-F456-4884-B049-85FD643ECFED");
        private static readonly Guid KeyboardCategory = new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");

        internal static ImeIdentity ReadDefault()
        {
            ITfInputProcessorProfiles profiles = Create();
            if (profiles == null)
            {
                return ImeIdentity.Unknown;
            }

            try
            {
                ushort language;
                int hr = profiles.GetCurrentLanguage(out language);
                if (hr < 0)
                {
                    HostLog.Warn("no composing language, hr=0x" + hr.ToString("x"));
                    return ImeIdentity.Unknown;
                }

                Guid category = KeyboardCategory;
                Guid clsid;
                Guid profileId;
                hr = profiles.GetDefaultLanguageProfile(language, ref category, out clsid, out profileId);
                if (hr < 0)
                {
                    HostLog.Warn(
                        "no default IME for language 0x" + language.ToString("x4") + ", hr=0x" + hr.ToString("x")
                    );
                    return ImeIdentity.Unknown;
                }

                return new ImeIdentity(Describe(profiles, clsid, language, profileId));
            }
            finally
            {
                if (Marshal.IsComObject(profiles))
                {
                    Marshal.ReleaseComObject(profiles);
                }
            }
        }

        private static ITfInputProcessorProfiles Create()
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
                return Marshal.GetObjectForIUnknown(unknown) as ITfInputProcessorProfiles;
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        private static string Describe(ITfInputProcessorProfiles profiles, Guid clsid, ushort language, Guid profileId)
        {
            IntPtr description;
            int hr = profiles.GetLanguageProfileDescription(ref clsid, language, ref profileId, out description);
            if (hr < 0 || description == IntPtr.Zero)
            {
                HostLog.Warn("no IME name, hr=0x" + hr.ToString("x"));
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
    }
}
