using System;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Selects TSF first, then Shell visuals with taskbar UIA. See ADR 0051.</summary>
    internal sealed class InputModeSwitch : IDisposable
    {
        private readonly IInputModePlatform _platform;
        private readonly Action<InputModeView> _publish;
        private LanguageProfile? _profile;
        private IInputModeSource _source;
        private InputModeView _view;
        private object _pending;
        private int _generation;
        private bool _disposed;

        internal InputModeSwitch(IInputModePlatform platform, Action<InputModeView> publish)
        {
            _platform = platform;
            _publish = publish;
        }

        internal void Refresh(LanguageProfile? profile)
        {
            if (_disposed)
                return;
            if (!Nullable.Equals(profile, _profile))
                Reset();
            _profile = profile;
            if (!Current)
            {
                Reset();
                return;
            }
            if (_source == null)
            {
                _source = _platform.OpenPublic(out bool absent);
                if (_source == null && absent)
                {
                    int generation = _generation;
                    _source = _platform.OpenShell(() =>
                    {
                        if (generation == _generation && Current)
                            Read();
                    });
                }
            }
            Read();
        }

        internal void Click()
        {
            if (_pending != null || !Current || _view?.Enabled != true)
                return;
            int generation = _generation;
            Read();
            if (generation != _generation || !Current || _view?.Enabled != true)
                return;
            object request = new object();
            _pending = request;
            Publish(_view.Disabled());
            try
            {
                _source.Click(() => Complete(request));
            }
            catch
            {
                Complete(request);
                throw;
            }
        }

        private void Complete(object request)
        {
            if (!ReferenceEquals(request, _pending))
                return;
            _pending = null;
            if (Current)
                Read();
        }

        private bool Current =>
            !_disposed
            && _platform.OwnsFocus
            && _profile.HasValue
            && Nullable.Equals(_profile, _platform.ActiveProfile);

        private void Read()
        {
            InputModeView view = _source?.Read();
            Publish(_pending != null ? view?.Disabled() : view);
        }

        internal void Reset()
        {
            _generation++;
            _pending = null;
            IInputModeSource source = _source;
            _source = null;
            _profile = null;
            source?.Dispose();
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
            _disposed = true;
            Reset();
        }
    }
}
