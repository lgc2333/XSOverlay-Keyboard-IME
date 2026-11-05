using System;
using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Candidates
{
    /// <summary>Reads the candidate page the IME published as a UI element.</summary>
    internal static class CandidateReader
    {
        private const int MaxItems = 10;

        // The IME updates its element on every keystroke, so a failure is logged once
        // until the read succeeds again.
        private static int _lastFailure;

        internal static CandidateSnapshot Read(ITfUIElementMgr manager, uint elementId)
        {
            if (manager == null)
            {
                FailOnce(1, "no element manager");
                return CandidateSnapshot.Empty;
            }

            ITfUIElement element;
            int hr = manager.GetUIElement(elementId, out element);
            if (hr < 0 || element == null)
            {
                FailOnce(2, "no element " + elementId + ", hr=0x" + hr.ToString("x"));
                return CandidateSnapshot.Empty;
            }

            var list = element as ITfCandidateListUIElement;
            if (list == null)
            {
                FailOnce(3, "element " + elementId + " is not a candidate list");
                return CandidateSnapshot.Empty;
            }

            _lastFailure = 0;

            uint count;
            uint selected;
            list.GetCount(out count);
            list.GetSelection(out selected);

            int limit = (int)Math.Min(count, MaxItems);
            var items = new string[limit];
            for (int index = 0; index < limit; index++)
            {
                IntPtr pointer;
                if (list.GetString((uint)index, out pointer) == 0 && pointer != IntPtr.Zero)
                {
                    items[index] = Marshal.PtrToStringBSTR(pointer);
                    Marshal.FreeBSTR(pointer);
                }
                else
                {
                    items[index] = string.Empty;
                }
            }

            var snapshot = new CandidateSnapshot(count, (int)selected, items);
            HostLog.Info("candidates " + snapshot.Describe());
            return snapshot;
        }

        private static void FailOnce(int reason, string message)
        {
            if (_lastFailure == reason)
            {
                return;
            }

            _lastFailure = reason;
            HostLog.Warn("candidate read: " + message);
        }
    }
}
