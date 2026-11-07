using System;

namespace XSOverlayKeyboardIme.Ime
{
    internal enum KeyboardInputType : uint
    {
        TextService = 1,
        KeyboardLayout = 2,
    }

    /// <summary>The identity TSF needs to activate a keyboard input. See ADR 0044.</summary>
    internal readonly struct LanguageProfile : IEquatable<LanguageProfile>
    {
        internal LanguageProfile(KeyboardInputType type, ushort language, Guid service, Guid profile, IntPtr layout)
        {
            Type = type;
            Language = language;
            Service = type == KeyboardInputType.TextService ? service : Guid.Empty;
            Profile = type == KeyboardInputType.TextService ? profile : Guid.Empty;
            Layout = type == KeyboardInputType.KeyboardLayout ? layout : IntPtr.Zero;
        }

        internal KeyboardInputType Type { get; }
        internal ushort Language { get; }
        internal Guid Service { get; }
        internal Guid Profile { get; }
        internal IntPtr Layout { get; }

        public bool Equals(LanguageProfile other)
        {
            return Type == other.Type
                && Language == other.Language
                && Service == other.Service
                && Profile == other.Profile
                && Layout == other.Layout;
        }

        public override bool Equals(object value)
        {
            return value is LanguageProfile profile && Equals(profile);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Language, Service, Profile, Layout);
        }
    }
}
