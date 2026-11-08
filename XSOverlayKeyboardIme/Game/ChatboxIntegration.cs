using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using XSOverlay;
using XSOverlayKeyboardIme.Chatbox;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Page;

namespace XSOverlayKeyboardIme.Game
{
    /// <summary>Uses XSOverlay's discovered target and installed OSC encoder.</summary>
    internal static class ChatboxIntegration
    {
        private static readonly object Gate = new object();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly UdpClient Udp = new UdpClient();
        private static readonly ChatboxBroadcast Broadcast = new ChatboxBroadcast(Send);
        private static VRChatOscService _service;
        private static VRChatOscTarget _target;
        private static int _generation;
        private static IPEndPoint _endpoint;
        private static int _submissions;
        private static bool _visible = true;

        internal static int Revision
        {
            get
            {
                lock (Gate)
                    return Broadcast.Revision;
            }
        }

        internal static void Update(ChatboxState state)
        {
            lock (Gate)
                Broadcast.Update(_visible && state.Active, state.Text, state.Submitting, state.Revision);
        }

        internal static void SetVisible(bool visible)
        {
            lock (Gate)
            {
                _visible = visible;
                if (!visible)
                    Hide();
            }
        }

        internal static void Hide()
        {
            lock (Gate)
            {
                Broadcast.Update(false, string.Empty, false, Broadcast.Revision);
                Broadcast.Pump(Clock.ElapsedMilliseconds);
            }
        }

        internal static void Pump()
        {
            lock (Gate)
            {
                var service = VRChatOscIntegration.Service as VRChatOscService;
                int generation = service?.TargetGeneration ?? 0;
                VRChatOscTarget target = service?.Target;
                if (!IsCurrentTarget(service, target, generation))
                {
                    _endpoint = null;
                    Broadcast.ResetDelivery();
                    return;
                }

                if (
                    !ReferenceEquals(_service, service)
                    || !ReferenceEquals(_target, target)
                    || _generation != generation
                    || _endpoint == null
                )
                {
                    _service = service;
                    _target = target;
                    _generation = generation;
                    _endpoint = target.OscEndPoint;
                    Broadcast.ResetDelivery();
                }
                Broadcast.Pump(Clock.ElapsedMilliseconds);
            }
        }

        internal static bool BeginSubmission(VRChatOscService service)
        {
            if (!ReferenceEquals(VRChatOscIntegration.Service, service))
                return false;
            lock (Gate)
            {
                _submissions++;
                Broadcast.BeginSubmission();
            }
            return true;
        }

        internal static async Task<VRChatChatboxSendResult> CompleteSubmission(Task<VRChatChatboxSendResult> result)
        {
            try
            {
                return await result.ConfigureAwait(false);
            }
            finally
            {
                lock (Gate)
                    if (--_submissions == 0)
                    {
                        Broadcast.EndSubmission(Clock.ElapsedMilliseconds);
                        int revision = Broadcast.Revision;
                        // Pre-submit Vuplex snapshots can arrive after the WebSocket send.
                        MainThreadDispatcher.Post(() => KeyboardPage.RefreshChatbox(revision));
                    }
            }
        }

        internal static void Stop()
        {
            Hide();
            Udp.Dispose();
        }

        private static void Send(string address, object[] arguments)
        {
            if (_endpoint == null || !IsCurrentTarget(_service, _target, _generation))
                return;
            try
            {
                using var stream = new MemoryStream();
                new uOSC.Message(address, arguments).Write(stream);
                byte[] data = stream.ToArray();
                Udp.Send(data, data.Length, _endpoint);
            }
            catch (Exception exception)
            {
                HostLog.Warn("ChatBox update failed: " + exception.Message);
            }
        }

        private static bool IsCurrentTarget(VRChatOscService service, VRChatOscTarget target, int generation) =>
            service != null
            && service.IsAvailable
            && target != null
            && ReferenceEquals(service, VRChatOscIntegration.Service)
            && ReferenceEquals(target, service.Target)
            && generation == service.TargetGeneration;
    }
}
