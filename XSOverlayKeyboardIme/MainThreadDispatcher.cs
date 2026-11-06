using System;
using System.Collections.Concurrent;
using XSOverlayKeyboardIme.Diagnostics;

namespace XSOverlayKeyboardIme
{
    /// <summary>
    /// Hands work from the host thread to Unity's main thread. Unity objects may only be
    /// touched there, so every page write goes through this queue.
    /// </summary>
    internal static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> Pending = new ConcurrentQueue<Action>();

        internal static void Post(Action action)
        {
            Pending.Enqueue(action);
        }

        internal static void Drain()
        {
            while (Pending.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    HostLog.Error("main thread work failed " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
    }
}
