using System;
using System.Runtime.InteropServices;
using System.Text;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Interop;

namespace XSOverlayKeyboardIme.Ime
{
    /// <summary>
    /// One UI-less input context from IngameIME. It reports the composing string, the
    /// candidates and the committed text as callbacks, all on the thread that created it.
    /// See ADR 0022 and ADR 0024.
    /// </summary>
    internal sealed class ImeContext : IDisposable
    {
        /// <summary>The composition state value that closes a composition.</summary>
        private const int CompositionEnd = 2;

        /// <summary>The candidate list state value that closes the list.</summary>
        private const int CandidateListEnd = 2;

        // Where the IME would place its own windows. The host window sits off screen and
        // the shim hides the candidate window, so only the box has to be well formed.
        private const int PreEditRectX = 0;
        private const int PreEditRectY = 0;
        private const int PreEditRectSize = 4;

        // ADR 0024: the native side keeps these function pointers until the process
        // ends, so each delegate is created once and never collected.
        private static readonly ImeShim.PreEditCallback PreEditRef = OnPreEdit;
        private static readonly ImeShim.CommitCallback CommitRef = OnCommit;
        private static readonly ImeShim.CandidatesCallback CandidatesRef = OnCandidates;

        /// <summary>The context the callbacks reach. Exactly one exists per process.</summary>
        private static ImeContext _current;

        private readonly IntPtr _handle;
        private volatile bool _composing;
        private bool _disposed;

        internal ImeContext(IntPtr window)
        {
            _handle = ImeShim.iime_create(window);
            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("IngameIME refused to create an input context");
            }

            // Register the callbacks before activation, so nothing is missed.
            _current = this;
            ImeShim.iime_set_preedit_rect(_handle, PreEditRectX, PreEditRectY, PreEditRectSize, PreEditRectSize);
            ImeShim.iime_set_callbacks(_handle, PreEditRef, CommitRef, CandidatesRef, IntPtr.Zero);
            ImeShim.iime_set_activated(_handle, 1);
            HostLog.Info("ime context ready");
        }

        /// <summary>Raised with the composing string, empty when the IME holds none, and
        /// the range the IME highlights inside it.</summary>
        internal event Action<string, int, int> PreEditChanged;

        /// <summary>Raised with the candidates and the index the IME highlights.</summary>
        internal event Action<string[], int> CandidatesChanged;

        internal event Action<string> Committed;

        /// <summary>True between the composition starting and the text being committed.</summary>
        internal bool Composing
        {
            get { return _composing; }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Stop routing first: the teardown reports events, and nothing may act on them.
            _current = null;
            ImeShim.iime_set_activated(_handle, 0);
            ImeShim.iime_destroy(_handle);
        }

        private void HandlePreEdit(int state, int selectionStart, int selectionEnd, IntPtr content)
        {
            _composing = state != CompositionEnd;
            string text = state == CompositionEnd ? string.Empty : Decode(content);
            int start = state == CompositionEnd ? 0 : Math.Max(0, selectionStart);
            int end = state == CompositionEnd ? 0 : Math.Max(start, selectionEnd);

            Action<string, int, int> handler = PreEditChanged;
            if (handler != null)
            {
                handler(text, start, end);
            }
        }

        private void HandleCandidates(int state, int selection, IntPtr items, int count)
        {
            bool ended = state == CandidateListEnd;
            string[] candidates = ended ? Array.Empty<string>() : ReadItems(items, count);

            Action<string[], int> handler = CandidatesChanged;
            if (handler != null)
            {
                handler(candidates, ended ? -1 : selection);
            }
        }

        private void HandleCommit(IntPtr text)
        {
            string committed = Decode(text);
            _composing = false;

            Action<string> handler = Committed;
            if (handler != null)
            {
                handler(committed);
            }
        }

        /// <summary>
        /// Runs a callback body. An exception must never cross the native boundary: it
        /// would end the process with STATUS_FATAL_USER_CALLBACK_EXCEPTION. See ADR 0024.
        /// </summary>
        private static void Guard(string name, Action<ImeContext> body)
        {
            try
            {
                ImeContext context = _current;
                if (context != null)
                {
                    body(context);
                }
            }
            catch (Exception ex)
            {
                HostLog.Error("the " + name + " callback failed " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void OnPreEdit(int state, int selectionStart, int selectionEnd, IntPtr content, IntPtr user)
        {
            Guard("pre-edit", context => context.HandlePreEdit(state, selectionStart, selectionEnd, content));
        }

        private static void OnCommit(IntPtr text, IntPtr user)
        {
            Guard("commit", context => context.HandleCommit(text));
        }

        private static void OnCandidates(int state, int selection, IntPtr items, int count, IntPtr user)
        {
            Guard("candidate", context => context.HandleCandidates(state, selection, items, count));
        }

        private static string[] ReadItems(IntPtr items, int count)
        {
            if (items == IntPtr.Zero || count <= 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[count];
            for (int index = 0; index < count; index++)
            {
                result[index] = Decode(Marshal.ReadIntPtr(items, index * IntPtr.Size));
            }

            return result;
        }

        /// <summary>
        /// Reads a UTF-8 string the native side owns. The runtime has no portable
        /// one-call decoder on this profile, so the length is counted first.
        /// </summary>
        private static string Decode(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
