using System;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Windows input-mode items and the host identity they belong to.</summary>
    internal interface IInputModePlatform
    {
        LanguageProfile? ActiveProfile { get; }
        bool OwnsFocus { get; }
        IInputModeSource OpenPublic(out bool absent);
        IInputModeSource OpenShell(Action changed);
    }

    /// <summary>Callbacks run on the host STA; disposal revokes pending work.</summary>
    internal interface IInputModeSource : IDisposable
    {
        InputModeView Read();
        void Click(Action complete);
    }
}
