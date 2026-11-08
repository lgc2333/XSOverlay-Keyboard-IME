using System;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Selects one source for the host's mode item and its click. See ADR 0042.</summary>
    internal sealed class InputModeButton : IDisposable
    {
        private readonly HostWindow _window;
        private readonly CompositionSession _session;
        private readonly Func<LanguageProfile?> _readProfile;
        private readonly Action<InputModeView> _publish;
        private LanguageProfile? _profile;
        private LanguageBarButton _public;
        private ShellModeButton _shell;
        private InputModeView _view;
        private bool _sampledShell;
        private int _pending;

        internal InputModeButton(
            HostWindow window,
            CompositionSession session,
            Func<LanguageProfile?> readProfile,
            Action<InputModeView> publish
        )
        {
            _window = window;
            _session = session;
            _readProfile = readProfile;
            _publish = publish;
        }

        internal void Refresh(LanguageProfile? profile)
        {
            if (!Nullable.Equals(profile, _profile))
                Reset();
            _profile = profile;
            if (!profile.HasValue)
                return;
            bool absent = false;
            if (_public == null)
                _public = LanguageBarButton.Open(out absent);
            if (_public != null)
            {
                Publish(_public.Read());
                return;
            }
            if (!absent)
                return;
            if (_shell != null)
            {
                ReadShell();
                return;
            }
            if (_sampledShell && Win32Native.GetForegroundWindow() != _window.Handle)
                return;
            if (!Begin())
                return;
            try
            {
                OpenShell();
                ReadShell();
                _sampledShell = true;
            }
            finally
            {
                _window.Post(HostWindow.ModeCompleteMessage);
            }
        }

        internal void Click()
        {
            // This guard precedes foreground, audio (on the page), and every native call.
            if (_view?.Enabled != true || !_profile.HasValue || !Nullable.Equals(_profile, _readProfile()))
                return;
            if (_public != null)
            {
                Publish(_public.Read());
                if (_view?.Enabled == true)
                {
                    _public.Click();
                    Publish(_public.Read());
                }
                return;
            }
        }

        private bool Begin()
        {
            if (!_session.BeginModeAction())
                return false;
            _pending++;
            return true;
        }

        private void OpenShell()
        {
            if (_shell == null)
                _shell = ShellModeButton.Open(_window.Handle, ReadShell);
            if (_shell == null)
                Publish(_view?.Disabled());
        }

        private void ReadShell()
        {
            if (_shell == null || !Nullable.Equals(_profile, _readProfile()))
                return;
            // Keep production display-only until the ADR 0050 lease is integrated.
            Publish(_shell.TryRead(out InputModeView view) ? view?.Disabled() : _view?.Disabled());
        }

        /// <summary>Collects queued Shell notifications before returning foreground.</summary>
        internal void Complete()
        {
            if (_pending == 0 || --_pending != 0)
                return;
            try
            {
                ReadShell();
                _shell?.Dispose();
                _shell = null;
            }
            finally
            {
                _session.EndModeAction();
            }
        }

        internal void Reset()
        {
            _public?.Dispose();
            _public = null;
            _shell?.Dispose();
            _shell = null;
            _profile = null;
            _sampledShell = false;
            Publish(null);
        }

        private void Publish(InputModeView view)
        {
            if (view?.Key == _view?.Key)
                return;
            _view = view;
            _publish(view);
        }

        public void Dispose()
        {
            Reset();
            _pending = 0;
            _session.EndModeAction();
        }
    }
}
