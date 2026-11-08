using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>Shell visuals on the STA, UIA discovery and Invoke on an MTA. See ADR 0051.</summary>
    internal sealed class TaskbarModeButton : IInputModeSource
    {
        private static readonly ImeShim.TaskbarRequestAllowed AllowedRef = Allowed;
        private static readonly ImeShim.TaskbarRequestAllowed AuthorizeRef = Authorize;
        private readonly HostWindow _window;
        private readonly Func<bool> _isCurrent;
        private ShellModeButton _shell;
        private InputModeView _view;
        private Request _request;
        private bool _available;
        private bool _disposed;

        private TaskbarModeButton(HostWindow window, Func<bool> isCurrent)
        {
            _window = window;
            _isCurrent = isCurrent;
        }

        internal static TaskbarModeButton Open(HostWindow window, Func<bool> isCurrent, Action changed)
        {
            var button = new TaskbarModeButton(window, isCurrent);
            button._shell = ShellModeButton.Open(window.Handle, () => window.Post(changed));
            if (button._shell == null)
                return null;
            try
            {
                button.Run(false, changed);
                return button;
            }
            catch
            {
                button.Dispose();
                throw;
            }
        }

        public InputModeView Read()
        {
            if (_disposed)
                return null;
            if (_shell.TryRead(out InputModeView view))
                _view = view;
            else
                _view = _view?.Disabled();
            return _available ? _view : _view?.Disabled();
        }

        public void Click(Action complete) => Run(true, complete);

        private void Run(bool click, Action complete)
        {
            if (_disposed || _request != null)
            {
                complete();
                return;
            }
            var request = new Request(_window, _isCurrent);
            _request = request;
            IntPtr host = _window.Handle;
            try
            {
                var worker = new Thread(() =>
                {
                    int result = unchecked((int)0x80004005);
                    int available = -1;
                    GCHandle reference = default;
                    try
                    {
                        reference = GCHandle.Alloc(request);
                        result = ImeShim.iime_taskbar_mode_run(
                            host,
                            click ? 1 : 0,
                            AllowedRef,
                            AuthorizeRef,
                            GCHandle.ToIntPtr(reference),
                            out available
                        );
                    }
                    catch (Exception exception)
                    {
                        HostLog.Warn("the input-mode request failed: " + exception.GetType().Name);
                    }
                    finally
                    {
                        if (reference.IsAllocated)
                            reference.Free();
                        _window.Post(() =>
                        {
                            if (_disposed || !ReferenceEquals(_request, request))
                                return;
                            _request = null;
                            if (available >= 0)
                                _available = available != 0;
                            if (result < 0 && !request.Cancelled)
                                HostLog.Warn("the input-mode request failed, hr=0x" + result.ToString("x"));
                            // A failed Invoke keeps an already discovered target clickable.
                            complete();
                        });
                    }
                })
                {
                    IsBackground = true,
                    Name = "XSOverlayKeyboardIme.TaskbarMode",
                };
                worker.SetApartmentState(ApartmentState.MTA);
                worker.Start();
            }
            catch
            {
                request.Cancel();
                _request = null;
                complete();
                throw;
            }
        }

        private static int Allowed(IntPtr user)
        {
            try
            {
                return ((Request)GCHandle.FromIntPtr(user).Target).Cancelled ? 0 : 1;
            }
            catch
            {
                return 0;
            }
        }

        private static int Authorize(IntPtr user)
        {
            try
            {
                return ((Request)GCHandle.FromIntPtr(user).Target).Authorize() ? 1 : 0;
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_request != null)
                _request.Cancel();
            _shell?.Dispose();
            _shell = null;
        }

        private sealed class Request
        {
            private readonly HostWindow _window;
            private readonly Func<bool> _isCurrent;
            private readonly TaskCompletionSource<bool> _authorization = new TaskCompletionSource<bool>();
            internal volatile bool Cancelled;

            internal Request(HostWindow window, Func<bool> isCurrent)
            {
                _window = window;
                _isCurrent = isCurrent;
            }

            internal bool Authorize()
            {
                if (
                    Cancelled
                    || !_window.Post(() =>
                    {
                        bool approved = false;
                        try
                        {
                            approved = !Cancelled && _isCurrent();
                        }
                        finally
                        {
                            _authorization.TrySetResult(approved);
                        }
                    })
                )
                    return false;
                return _authorization.Task.GetAwaiter().GetResult();
            }

            internal void Cancel()
            {
                Cancelled = true;
                _authorization.TrySetResult(false);
            }
        }
    }
}
