using BepInEx.Logging;

namespace XSOverlayKeyboardIme.Diagnostics
{
    /// <summary>Single place the host writes to, so every line carries one prefix.</summary>
    internal static class HostLog
    {
        private const string Prefix = "[ime] ";
        private static ManualLogSource _sink;

        internal static void Attach(ManualLogSource sink)
        {
            _sink = sink;
        }

        internal static void Info(string message)
        {
            _sink?.LogInfo(Prefix + message);
        }

        internal static void Warn(string message)
        {
            _sink?.LogWarning(Prefix + message);
        }

        internal static void Error(string message)
        {
            _sink?.LogError(Prefix + message);
        }
    }
}
