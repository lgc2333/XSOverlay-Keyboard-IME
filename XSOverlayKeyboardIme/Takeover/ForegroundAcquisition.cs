using System;

namespace XSOverlayKeyboardIme.Takeover
{
    internal readonly struct WindowIdentity
    {
        internal WindowIdentity(IntPtr handle, uint process, uint thread)
        {
            Handle = handle;
            Process = process;
            Thread = thread;
        }

        internal IntPtr Handle { get; }
        private uint Process { get; }
        private uint Thread { get; }

        internal bool Matches(WindowIdentity? other) =>
            other.HasValue
            && Handle == other.Value.Handle
            && Process == other.Value.Process
            && Thread == other.Value.Thread;
    }

    internal interface IForegroundPlatform
    {
        IntPtr Foreground { get; }
        IntPtr Focus { get; }
        WindowIdentity? Identify(IntPtr window);
        bool RequestForeground(IntPtr window);
        void FocusWindow(IntPtr window);
        bool KeysAndMenusIdle(IntPtr original);
        uint InsertAltPair();
    }

    /// <summary>One explicit acquisition, then observation only. Alt is limited by ADR 0055.</summary>
    internal sealed class ForegroundAcquisition
    {
        private readonly IForegroundPlatform _api;
        private readonly IntPtr _host;
        private WindowIdentity? _previous;

        internal ForegroundAcquisition(IForegroundPlatform api, IntPtr host)
        {
            _api = api;
            _host = host;
        }

        internal bool Ensure() => _api.Foreground == _host && _api.Focus == _host;

        internal bool Acquire(Func<bool> valid)
        {
            if (!valid())
                return false;
            var host = _api.Identify(_host);
            if (!host.HasValue || !valid())
                return false;
            var original = _api.Identify(_api.Foreground);
            _previous = original.HasValue && original.Value.Handle != _host ? original : null;
            // Normal acquisition has no idle-key/menu prerequisite.
            bool normal = _api.RequestForeground(_host);
            if (!valid())
                return false;
            if (_api.Foreground == _host)
            {
                _api.FocusWindow(_host);
                return valid() && Ensure();
            }
            // A successful native return with activation not yet observed is not denial.
            if (normal || !original.HasValue || !Unchanged(host.Value, original.Value, valid))
                return false;
            if (!_api.KeysAndMenusIdle(original.Value.Handle) || !Unchanged(host.Value, original.Value, valid))
                return false;
            if (_api.InsertAltPair() != 2 || !Unchanged(host.Value, original.Value, valid))
                return false;
            _api.RequestForeground(_host);
            if (!valid())
                return false;
            if (_api.Foreground == _host)
                _api.FocusWindow(_host);
            return valid() && Ensure();
        }

        private bool Unchanged(WindowIdentity host, WindowIdentity original, Func<bool> valid) =>
            valid()
            && _api.Foreground == original.Handle
            && host.Matches(_api.Identify(_host))
            && original.Matches(_api.Identify(original.Handle))
            && valid();

        internal void Restore()
        {
            var previous = _previous;
            _previous = null;
            if (
                previous.HasValue
                && _api.Foreground == _host
                && previous.Value.Matches(_api.Identify(previous.Value.Handle))
                && _api.Foreground == _host
            )
                _api.RequestForeground(previous.Value.Handle);
        }
    }
}
