using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;
using XSOverlayKeyboardIme.Sinks;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>Takes the thread manager and keeps the UI element sink advised on it.</summary>
    internal sealed class ThreadManagerBridge : IDisposable
    {
        private readonly ITfThreadMgr _threadManager;
        private readonly ITfSource _source;
        private readonly UIElementSink _sink;
        private readonly uint _cookie;
        private bool _disposed;

        private ThreadManagerBridge(
            ITfThreadMgr threadManager,
            ITfSource source,
            UIElementSink sink,
            ITfUIElementMgr elementManager,
            uint cookie
        )
        {
            _threadManager = threadManager;
            _source = source;
            _sink = sink;
            _cookie = cookie;
            ElementManager = elementManager;
        }

        internal ITfUIElementMgr ElementManager { get; }

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

            return new ThreadManagerBridge(threadManager, source, sink, elementManager, cookie);
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
