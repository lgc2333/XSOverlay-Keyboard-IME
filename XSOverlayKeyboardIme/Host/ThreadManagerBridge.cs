using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Sinks;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>Owns the thread's TSF handles: the element manager, the advised sink and the IME open state.</summary>
    internal sealed class ThreadManagerBridge : IDisposable
    {
        private readonly ITfThreadMgr _threadManager;
        private readonly ITfSource _source;
        private readonly UIElementSink _sink;
        private readonly ImeOpenCompartment _imeOpen;
        private readonly uint _cookie;
        private bool _disposed;

        private ThreadManagerBridge(
            ITfThreadMgr threadManager,
            ITfSource source,
            UIElementSink sink,
            ITfUIElementMgr elementManager,
            uint cookie,
            ImeOpenCompartment imeOpen
        )
        {
            _threadManager = threadManager;
            _source = source;
            _sink = sink;
            _cookie = cookie;
            _imeOpen = imeOpen;
            ElementManager = elementManager;
        }

        internal ITfUIElementMgr ElementManager { get; }

        /// <summary>Null when the thread manager exposes no keyboard compartment.</summary>
        internal ImeOpenCompartment ImeOpen
        {
            get { return _imeOpen; }
        }

        internal static ThreadManagerBridge Open(IUIElementObserver observer)
        {
            IntPtr raw;
            int hr = Win32Native.TF_GetThreadMgr(out raw);
            if (hr < 0 || raw == IntPtr.Zero)
            {
                HostLog.Error("no thread manager, hr=0x" + hr.ToString("x"));
                return null;
            }

            ITfThreadMgr threadManager;
            try
            {
                threadManager = Marshal.GetObjectForIUnknown(raw) as ITfThreadMgr;
            }
            finally
            {
                Marshal.Release(raw);
            }

            if (threadManager == null)
            {
                HostLog.Error("thread manager does not expose ITfThreadMgr");
                return null;
            }

            int clientId;
            int activateHr = threadManager.Activate(out clientId);
            if (activateHr < 0)
            {
                HostLog.Error("thread manager does not activate, hr=0x" + activateHr.ToString("x"));
                return null;
            }

            HostLog.Info("thread manager activated, client=" + clientId);

            var elementManager = threadManager as ITfUIElementMgr;
            var source = threadManager as ITfSource;
            HostLog.Info("thread manager ready, element manager=" + (elementManager != null));

            if (source == null)
            {
                HostLog.Error("thread manager does not expose ITfSource");
                return null;
            }

            var sink = new UIElementSink(observer);
            IntPtr unknown = Marshal.GetComInterfaceForObject(sink, typeof(ITfUIElementSink));
            uint cookie;
            try
            {
                Guid iid = typeof(ITfUIElementSink).GUID;
                int adviseHr = source.AdviseSink(ref iid, unknown, out cookie);
                HostLog.Info("UIElementSink advised, hr=0x" + adviseHr.ToString("x"));
                if (adviseHr < 0)
                {
                    return null;
                }
            }
            finally
            {
                Marshal.Release(unknown);
            }

            var imeOpen = ImeOpenCompartment.Attach(threadManager, clientId);
            return new ThreadManagerBridge(threadManager, source, sink, elementManager, cookie, imeOpen);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_cookie != 0)
            {
                try
                {
                    _source.UnadviseSink(_cookie);
                }
                catch (Exception ex)
                {
                    HostLog.Warn("UnadviseSink failed: " + ex.GetType().Name);
                }
            }

            Release(_sink);
            Release(_source);

            if (_imeOpen != null)
            {
                _imeOpen.Dispose();
            }

            Release(_threadManager);
        }

        private static void Release(object instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                Marshal.ReleaseComObject(instance);
            }
        }
    }
}
