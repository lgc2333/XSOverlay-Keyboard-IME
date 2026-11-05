using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.SelfCheck
{
    /// <summary>Window messages only the self-check posts to itself.</summary>
    internal static class SelfCheckMessages
    {
        /// <summary>Second look at the composing string, then commit.</summary>
        internal const uint Observe = Win32Native.WmApp + 3;
    }
}
