using System.Runtime.InteropServices;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Sinks
{
    /// <summary>
    /// Classic COM-callable wrapper subscribed to ITfUIElementMgr. The runtime calls it
    /// on the IME thread, so it only records the event and posts it onward. See ADR 0003.
    /// </summary>
    [ComVisible(true)]
    internal sealed class UIElementSink : ITfUIElementSink
    {
        private readonly IUIElementObserver _observer;

        internal UIElementSink(IUIElementObserver observer)
        {
            _observer = observer;
        }

        public int BeginUIElement(uint id, out bool show)
        {
            // The keyboard page renders its own bar, so the IME window stays hidden.
            show = false;
            Guard(() => _observer.ElementBegun(id));
            return 0;
        }

        public int UpdateUIElement(uint id)
        {
            Guard(() => _observer.ElementUpdated(id));
            return 0;
        }

        public int EndUIElement(uint id)
        {
            Guard(() => _observer.ElementEnded(id));
            return 0;
        }

        /// <summary>An exception here would cross the COM boundary and kill the process.</summary>
        private static void Guard(System.Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // Deliberately swallowed: the IME must not see a managed exception.
            }
        }
    }
}
