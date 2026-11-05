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

        internal static CandidateSnapshot Read(ITfUIElementMgr manager, uint elementId)
        {
            if (manager == null)
            {
                return CandidateSnapshot.Empty;
            }

            ITfUIElement element;
            if (manager.GetUIElement(elementId, out element) != 0 || element == null)
            {
                return CandidateSnapshot.Empty;
            }

            var list = element as ITfCandidateListUIElement;
            if (list == null)
            {
                return CandidateSnapshot.Empty;
            }

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
    }
}
