namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>One IME the page offers to switch to: the name it draws and the profile behind it.</summary>
    internal sealed class ImeChoice
    {
        internal ImeChoice(string name, LanguageProfile profile)
        {
            Name = name;
            Profile = profile;
        }

        internal string Name { get; }

        internal LanguageProfile Profile { get; }
    }
}
