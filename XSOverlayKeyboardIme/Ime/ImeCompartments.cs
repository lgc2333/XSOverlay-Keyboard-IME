using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// The keyboard compartment the IME keeps its input mode in. Writing it flips the
    /// mode instead of setting it, so the value written must differ from the value
    /// stored: writing the same value twice flips twice. See ADR 0025.
    /// </summary>
    internal sealed class ImeCompartments : IDisposable
    {
        private static readonly Guid KeyboardOpenClose = new Guid("58273AAD-01BB-4164-95C6-755BA0B5162D");

        private readonly ITfThreadMgr _threadManager;
        private readonly ITfCompartment _compartment;
        private readonly int _clientId;
        private bool _disposed;

        private ImeCompartments(ITfThreadMgr threadManager, ITfCompartment compartment, int clientId)
        {
            _threadManager = threadManager;
            _compartment = compartment;
            _clientId = clientId;
        }

        /// <summary>
        /// Joins the thread's TSF client, which IngameIME has already created. Returns
        /// null when the compartment is unreachable, which only costs the flip.
        /// </summary>
        internal static ImeCompartments Open()
        {
            IntPtr raw;
            int hr = Win32Native.TF_GetThreadMgr(out raw);
            if (hr < 0 || raw == IntPtr.Zero)
            {
                HostLog.Warn("no TSF thread manager, hr=0x" + hr.ToString("x") + "; the mode cannot be flipped");
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
                HostLog.Warn("the thread manager is not ITfThreadMgr; the mode cannot be flipped");
                return null;
            }

            int clientId;
            int activated = threadManager.Activate(out clientId);
            if (activated < 0)
            {
                HostLog.Warn("the thread manager does not activate, hr=0x" + activated.ToString("x"));
                Release(threadManager);
                return null;
            }

            var manager = threadManager as ITfCompartmentMgr;
            ITfCompartment compartment = null;
            int opened = -1;
            if (manager != null)
            {
                Guid key = KeyboardOpenClose;
                opened = manager.GetCompartment(ref key, out compartment);
            }

            if (manager == null || opened < 0 || compartment == null)
            {
                HostLog.Warn("no keyboard compartment, hr=0x" + opened.ToString("x"));
                threadManager.Deactivate();
                Release(threadManager);
                return null;
            }

            HostLog.Info("keyboard compartment ready, client=" + clientId);
            return new ImeCompartments(threadManager, compartment, clientId);
        }

        /// <summary>
        /// Writes the value the compartment does not hold. Whether the IME acts on that
        /// write is up to the IME; the caller waits for the mode report to find out.
        /// </summary>
        internal bool TryFlip()
        {
            int current;
            if (!TryRead(out current))
            {
                HostLog.Warn("the keyboard compartment could not be read; the mode stays as it is");
                return false;
            }

            // A stored value that does not change may not reach the IME at all.
            var flipped = new NativeVariant { Type = NativeVariant.TypeInt32, Integer = current == 0 ? 1 : 0 };
            int hr = _compartment.SetValue(_clientId, ref flipped);
            HostLog.Info("ime mode flip " + current + " -> " + flipped.Integer + ", hr=0x" + hr.ToString("x"));
            return hr >= 0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Release(_compartment);

            try
            {
                _threadManager.Deactivate();
            }
            catch (Exception ex)
            {
                HostLog.Warn("the TSF client did not deactivate: " + ex.GetType().Name);
            }

            Release(_threadManager);
        }

        /// <summary>Never folds a failed read into a value, so the log stays truthful.</summary>
        private bool TryRead(out int value)
        {
            value = 0;
            int hr = _compartment.GetValue(out NativeVariant variant);
            if (hr < 0)
            {
                return false;
            }

            value = variant.Integer;
            return true;
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
