namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>One IME the page offers to switch to: the name it draws and the profile behind it.</summary>
    internal sealed class ImeChoice
    {
        internal ImeChoice(string name, string language, LanguageProfile profile)
        {
            Name = name;
            Language = language;
            Profile = profile;
        }

        internal string Name { get; }

        /// <summary>The language the profile is registered under, for telling equal names apart.</summary>
        internal string Language { get; }

        internal LanguageProfile Profile { get; }
    }
}
