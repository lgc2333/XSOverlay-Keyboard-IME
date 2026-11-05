using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Holds GUID_COMPARTMENT_KEYBOARD_OPENCLOSE so the host can force the IME open, and
    /// puts the state back on release. See ADR 0006's rule about restoring what was taken.
    /// </summary>
    internal sealed class ImeOpenCompartment : IDisposable
    {
        private static readonly Guid KeyboardOpenClose = new Guid("58273AAD-01BB-4164-95C6-755BA0B5162D");

        private readonly ITfCompartment _compartment;
        private readonly int _clientId;
        private NativeVariant _previous;
        private bool _captured;

        private ImeOpenCompartment(ITfCompartment compartment, int clientId)
        {
            _compartment = compartment;
            _clientId = clientId;
        }

        /// <summary>Returns null when the thread manager exposes no keyboard compartment.</summary>
        internal static ImeOpenCompartment Attach(ITfThreadMgr threadManager, int clientId)
        {
            var compartments = threadManager as ITfCompartmentMgr;
            if (compartments == null)
            {
                HostLog.Error("thread manager does not expose ITfCompartmentMgr");
                return null;
            }

            Guid key = KeyboardOpenClose;
            ITfCompartment compartment;
            int hr = compartments.GetCompartment(ref key, out compartment);
            if (hr < 0 || compartment == null)
            {
                HostLog.Error("no keyboard compartment, hr=0x" + hr.ToString("x"));
                return null;
            }

            return new ImeOpenCompartment(compartment, clientId);
        }

        /// <summary>Forces the open state and reports what the compartment held before.</summary>
        internal void Open()
        {
            _captured = TryRead(out _previous);
            string before = _captured ? Describe(_previous) : "unreadable";

            var opened = new NativeVariant { Type = NativeVariant.TypeInt32, Integer = 1 };
            int hr = _compartment.SetValue(_clientId, ref opened);

            string after = TryRead(out NativeVariant now) ? Describe(now) : "unreadable";
            HostLog.Info("ime open before=" + before + " set hr=0x" + hr.ToString("x") + " after=" + after);
        }

        public void Dispose()
        {
            if (_captured)
            {
                var previous = _previous;
                int hr = _compartment.SetValue(_clientId, ref previous);
                HostLog.Info("ime open restored to " + Describe(previous) + ", hr=0x" + hr.ToString("x"));
            }

            if (Marshal.IsComObject(_compartment))
            {
                Marshal.ReleaseComObject(_compartment);
            }
        }

        /// <summary>Never folds a failed read into a value, so the log stays truthful.</summary>
        private bool TryRead(out NativeVariant value)
        {
            return _compartment.GetValue(out value) >= 0;
        }

        private static string Describe(NativeVariant value)
        {
            return "vt=" + value.Type + " value=" + value.Integer;
        }
    }
}
