using System;
using System.Threading;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Host
{
    /// <summary>
    /// Runs the composition host on its own STA thread and routes host messages to the
    /// self-check run. It owns teardown so a failure still hands the foreground back.
    /// </summary>
    internal sealed class CompositionHost
    {
        /// <summary>XSOverlay finishes starting after the plugin, and steals the foreground while it does.</summary>
        private const int StartupSettleMilliseconds = 8000;

        private readonly string _typingText;
        private HostWindow _window;
        private ForegroundLease _lease;
        private ThreadManagerBridge _bridge;
        private SelfCheckRun _run;
        private bool _finished;

        private CompositionHost(string typingText)
        {
            _typingText = typingText;
        }

        internal static void Start(string typingText)
        {
            var host = new CompositionHost(typingText);
            var thread = new Thread(host.Run) { IsBackground = true, Name = "XSOverlayKeyboardIme" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private void Run()
        {
            try
            {
                Win32Native.SetErrorMode(Win32Native.SemFailCriticalErrors | Win32Native.SemNoOpenFileErrorBox);
                int hr = Win32Native.CoInitializeEx(IntPtr.Zero, Win32Native.CoInitApartmentThreaded);
                if (hr < 0)
                {
                    HostLog.Error("CoInitializeEx failed 0x" + hr.ToString("x"));
                    return;
                }

                Thread.Sleep(StartupSettleMilliseconds);
                _window = HostWindow.Create(OnHostMessage);
                _lease = new ForegroundLease(_window.Handle, _window.Editor);
                var feeder = new KeyFeeder(_lease);
                _run = new SelfCheckRun(_window, feeder, _typingText, Finish);

                _bridge = ThreadManagerBridge.Open(_run);
                if (_bridge == null)
                {
                    return;
                }

                _window.PumpPending();
                _lease.BeginAttach();
                _lease.Acquire();
                _run.Start(_bridge);
                _window.PumpMessages();
            }
            catch (Exception ex)
            {
                HostLog.Error("host thread failed " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                Finish();
                Win32Native.CoUninitialize();
            }
        }

        private void OnHostMessage(uint message)
        {
            if (_run != null)
            {
                _run.HandleMessage(message);
            }
        }

        private void Finish()
        {
            if (_finished)
            {
                return;
            }

            _finished = true;

            if (_bridge != null)
            {
                _bridge.Dispose();
            }

            if (_lease != null)
            {
                _lease.Restore();
                _lease.EndAttach();
            }

            if (_window != null)
            {
                _window.Close();
            }

            Win32Native.PostQuitMessage(0);
        }
    }
}
