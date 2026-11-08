using System;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>The host STA's native mode-item access.</summary>
    internal sealed class InputModePlatform : IInputModePlatform
    {
        private readonly HostWindow _window;
        private readonly Func<LanguageProfile?> _readProfile;

        internal InputModePlatform(HostWindow window, Func<LanguageProfile?> readProfile)
        {
            _window = window;
            _readProfile = readProfile;
        }

        public LanguageProfile? ActiveProfile => _readProfile();
        public bool OwnsFocus =>
            Win32Native.GetForegroundWindow() == _window.Handle && Win32Native.GetFocus() == _window.Handle;

        public IInputModeSource OpenPublic(out bool absent) => LanguageBarButton.Open(out absent);

        public IInputModeSource OpenShell(Action changed)
        {
            LanguageProfile? profile = ActiveProfile;
            return TaskbarModeButton.Open(_window, () => OwnsFocus && Nullable.Equals(profile, ActiveProfile), changed);
        }
    }
}
