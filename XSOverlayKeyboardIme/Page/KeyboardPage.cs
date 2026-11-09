using System;
using Vuplex.WebView;
using XSOverlayKeyboardIme.Chatbox;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Game;
using XSOverlayKeyboardIme.Ime;
using XSOverlayKeyboardIme.Takeover;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>
    /// The keyboard page as this plugin uses it: inject the bundle, write committed text
    /// into the composer and show the composition. Every member runs on Unity's main
    /// thread, and the page is looked up on demand. See ADR 0004 and ADR 0008.
    /// </summary>
    internal static class KeyboardPage
    {
        private static EventHandler<EventArgs<string>> _messageHandler;
        private static IWebView _listening;
        private static IWebView _injected;
        private static bool _confirmed;
        private static bool _missingLogged;
        private static CandidateScrollArea _scrollArea;
        private static long _focusGeneration;
        private static string _focusDocument;
        private static bool _bindingFocus;
        private static bool _focusBound;
        private static bool _visible = true;

        internal static CandidateScrollArea ScrollAreaFor(IWebView page) =>
            page != null && !page.IsDisposed && ReferenceEquals(page, _listening) ? _scrollArea : null;

        internal static Action<FocusMessage> FocusMessageReceived;
        internal static Action<string> FocusDocumentBound;
        internal static Action<ChatboxState> ChatboxChanged;
        internal static Action PageUnavailable;

        /// <summary>What the bar shows right now, replayed onto a page that appears later.</summary>
        private static CompositionView _view = CompositionView.Idle();

        /// <summary>Whether the composer holds the focus, and which IME it composes with. See ADR 0035.</summary>
        private static ComposerState _composerState = ComposerState.Hidden;

        /// <summary>The IME's input-mode icon, replayed onto a page that appears later.</summary>
        private static InputModeView _switchIcon;

        private static string _keyboardFormat;

        /// <summary>Receives the physical layout XSOverlay generated. See ADR 0049.</summary>
        internal static void ShowKeyboardFormat(string format)
        {
            _keyboardFormat = format;
            IWebView page = EnsureInjected();
            if (page != null)
                PushKeyboardFormat(page);
        }

        /// <summary>Shows the IME's input-mode icon, the one the page's switch button draws.</summary>
        internal static void ShowSwitchIcon(InputModeView icon)
        {
            _switchIcon = icon;
            IWebView page = EnsureInjected();
            if (page != null)
            {
                PushSwitchIcon(page);
            }
        }

        /// <summary>Shows a composition or the idle bar, and remembers it for a later page.</summary>
        internal static void Show(CompositionView view)
        {
            _view = view;
            IWebView page = EnsureInjected();
            if (page != null)
            {
                ShowBar(page, view);
            }
        }

        /// <summary>
        /// Shows the IME name and the switch beside the composer, or nothing when it has no
        /// focus, and remembers it for a later page. See ADR 0035.
        /// </summary>
        internal static void ShowComposerState(ComposerState state)
        {
            _composerState = state;
            IWebView page = EnsureInjected();
            if (page != null)
            {
                PushComposerState(page);
            }
        }

        /// <summary>
        /// Replays the current state when the keyboard opens or a page first becomes ready.
        /// </summary>
        internal static void Attach()
        {
            IWebView page = EnsureInjected();
            if (page == null)
            {
                return;
            }

            PushKeyboardFormat(page);
            ShowBar(page, _view);
            PushSwitchIcon(page);
            PushComposerState(page);
            RefreshChatbox(ChatboxIntegration.Revision);
            BindFocus(page);
        }

        internal static void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
                InvalidateFocus();
        }

        private static void InvalidateFocus()
        {
            string previous = _focusDocument;
            _focusGeneration++;
            _focusDocument = Guid.NewGuid().ToString("N");
            _bindingFocus = false;
            _focusBound = false;
            PageUnavailable?.Invoke();
            if (previous != null && _listening != null && !_listening.IsDisposed)
                _listening.ExecuteJavaScript(CallVoid("unbindFocus", JavaScriptJson.Serialize(previous)), _ => { });
        }

        private static void BindFocus(IWebView page)
        {
            if (!_visible || !_confirmed || _bindingFocus || _focusBound)
                return;
            if (_focusDocument == null)
                InvalidateFocus();
            long generation = _focusGeneration;
            string document = _focusDocument;
            _bindingFocus = true;
            page.ExecuteJavaScript(
                "(function(){var a=window.__xsoKeyboardIme;return a?a.focusDocument:''})()",
                nonce =>
                {
                    if (!CurrentBinding(page, document, generation))
                        return;
                    if (string.IsNullOrEmpty(nonce))
                    {
                        _bindingFocus = false;
                        return;
                    }
                    string arguments =
                        JavaScriptJson.Serialize(document) + "," + generation + "," + JavaScriptJson.Serialize(nonce);
                    page.ExecuteJavaScript(
                        Wrap("bindFocus", "return a.bindFocus(" + arguments + ")?'bound':'stale'"),
                        outcome =>
                        {
                            if (!CurrentBinding(page, document, generation))
                                return;
                            _bindingFocus = false;
                            if (outcome != "bound")
                                return;
                            _focusBound = true;
                            FocusDocumentBound?.Invoke(document);
                        }
                    );
                }
            );
        }

        private static bool CurrentBinding(IWebView page, string document, long generation) =>
            _visible
            && !page.IsDisposed
            && ReferenceEquals(page, _listening)
            && document == _focusDocument
            && generation == _focusGeneration;

        internal static void ShowFocus(string kind, FocusTicket ticket)
        {
            IWebView page = _listening;
            if (!_visible || !_focusBound || page == null || page.IsDisposed || ticket.Document != _focusDocument)
                return;
            // Never reinject or replay admission operations onto a different document.
            page.ExecuteJavaScript(
                CallVoid(
                    "applyFocus",
                    JavaScriptJson.Serialize(
                        new
                        {
                            kind,
                            document = ticket.Document,
                            revision = ticket.Revision,
                            request = ticket.Request,
                        }
                    )
                ),
                _ => { }
            );
        }

        internal static void RefreshChatbox(int revision)
        {
            if (_listening != null && !_listening.IsDisposed)
                Call(
                    _listening,
                    "ChatBox state",
                    CallVoid("refreshChatbox", JavaScriptJson.Serialize(revision)),
                    _ => { }
                );
        }

        /// <summary>Writes committed text into the composer. See ADR 0008.</summary>
        internal static void WriteBack(string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            IWebView page = EnsureInjected();
            if (page == null)
            {
                HostLog.Warn("the keyboard page is gone; dropping the writeback '" + text + "'");
                return;
            }

            Call(
                page,
                "writeback",
                CallReturning("insertText", JavaScriptJson.Serialize(text)),
                outcome =>
                {
                    if (outcome == null || outcome.StartsWith("error", StringComparison.Ordinal))
                    {
                        HostLog.Warn("writeback '" + text + "' -> " + outcome);
                        return;
                    }

                    HostLog.Info("writeback '" + text + "' -> " + outcome);
                }
            );
        }

        /// <summary>Calls a page function whose result matters; the sentinel means the bundle is gone.</summary>
        private static string CallReturning(string method, string arguments)
        {
            return Wrap(method, "return a." + method + "(" + arguments + ")");
        }

        /// <summary>Calls a page function that answers nothing; the sentinel means the bundle is gone.</summary>
        private static string CallVoid(string method, string arguments)
        {
            return Wrap(method, "try{a." + method + "(" + arguments + ");return 'ok'}catch(e){return 'error:' + e}");
        }

        private static string Wrap(string method, string body)
        {
            return "(function(){var a=window.__xsoKeyboardIme;if(!a||!a."
                + method
                + ")return '"
                + PageScript.Missing
                + "';"
                + body
                + "})()";
        }

        /// <summary>Finds the page and makes sure it runs the bundle; null while it does not exist.</summary>
        private static IWebView EnsureInjected()
        {
            if (!_visible)
                return null;
            IWebView page = KeyboardOverlayScope.FindPage();
            if (page == null || page.IsDisposed)
            {
                if (_focusBound || _bindingFocus)
                    InvalidateFocus();
                if (!_missingLogged)
                {
                    _missingLogged = true;
                    HostLog.Info("the keyboard page does not exist yet; the bar waits for it");
                }

                return null;
            }

            _missingLogged = false;
            Listen(page);
            Inject(page);
            return page;
        }

        private static void Inject(IWebView page)
        {
            // The page confirms the bundle with its ready message. Until that arrives the
            // injection may have landed on an unloaded page, so it is worth repeating.
            if (ReferenceEquals(page, _injected) && _confirmed)
            {
                return;
            }

            bool repeat = ReferenceEquals(page, _injected);
            _injected = page;
            PageScript.Inject(
                page,
                outcome =>
                {
                    if (repeat)
                    {
                        HostLog.Info("page script injected again; the page has not confirmed yet");
                        return;
                    }

                    HostLog.Info("page script injected -> " + outcome);
                }
            );
        }

        private static void PushKeyboardFormat(IWebView page)
        {
            Call(
                page,
                "keyboard layout",
                CallVoid("setKeyboardFormat", JavaScriptJson.Serialize(_keyboardFormat)),
                outcome =>
                {
                    if (outcome != "ok")
                        HostLog.Warn("the keyboard layout did not reach the page: " + outcome);
                }
            );
        }

        private static void PushSwitchIcon(IWebView page)
        {
            InputModeView icon = _switchIcon;

            // A null icon removes the switch, including one from the previous IME. See ADR 0042.
            Call(
                page,
                "input mode",
                CallVoid("showSwitchIcon", SwitchIconPayload.Build(icon)),
                outcome =>
                {
                    if (outcome != "ok")
                    {
                        HostLog.Warn("the input-mode icon did not reach the page: " + outcome);
                    }
                }
            );
        }

        private static void PushComposerState(IWebView page)
        {
            Call(
                page,
                "composer state",
                CallVoid("showComposerState", ComposerStatePayload.Build(_composerState)),
                outcome =>
                {
                    if (outcome != "ok")
                    {
                        HostLog.Warn("the composer state did not reach the page: " + outcome);
                    }
                }
            );
        }

        private static void ShowBar(IWebView page, CompositionView view)
        {
            Call(
                page,
                "composition",
                CallVoid("showComposition", CompositionPayload.Build(view)),
                outcome =>
                {
                    if (outcome != "ok")
                    {
                        HostLog.Warn("the composition did not reach the page: " + outcome);
                    }
                }
            );
        }

        /// <summary>Calls one page function, injecting the bundle when the page lost it.</summary>
        private static void Call(
            IWebView page,
            string label,
            string expression,
            Action<string> onOutcome,
            bool canInject = true
        )
        {
            if (page == null || page.IsDisposed)
            {
                HostLog.Warn("the keyboard page is gone; dropping the " + label);
                return;
            }

            bool injected = ReferenceEquals(page, _injected);
            string script = injected ? expression : PageScript.Source() + ";\n" + expression;
            _injected = page;

            page.ExecuteJavaScript(
                script,
                outcome =>
                {
                    if (outcome != null && outcome.Contains(PageScript.Missing) && canInject)
                    {
                        // The page reloaded since the last injection, so inject once more.
                        _injected = null;
                        Inject(page);
                        Listen(page);
                        Call(page, label, expression, onOutcome, canInject: false);
                        return;
                    }

                    onOutcome(outcome);
                }
            );
        }

        private static void Listen(IWebView page)
        {
            if (ReferenceEquals(page, _listening))
            {
                return;
            }

            IWebView previous = _listening;
            InvalidateFocus();
            _scrollArea = null;
            _listening = page;
            _messageHandler ??= OnMessage;
            if (previous != null && !previous.IsDisposed)
            {
                previous.MessageEmitted -= _messageHandler;
                previous.LoadProgressChanged -= OnLoadProgress;
            }

            page.MessageEmitted += _messageHandler;
            page.LoadProgressChanged += OnLoadProgress;
        }

        private static void OnLoadProgress(object sender, ProgressChangedEventArgs args)
        {
            if (args.Type == ProgressChangeType.Started)
            {
                if (!ReferenceEquals(sender, _listening))
                    return;
                InvalidateFocus();
                _scrollArea = null;
                _confirmed = false;
            }
        }

        private static void OnMessage(object sender, EventArgs<string> args)
        {
            if (!ReferenceEquals(sender, _listening))
                return;
            string message = args.Value;
            FocusTicket? source = null;
            const string focusPrefix = "xso-keyboard-ime focus ";
            if (message.StartsWith(focusPrefix, StringComparison.Ordinal))
            {
                try
                {
                    FocusMessage focus = Newtonsoft.Json.JsonConvert.DeserializeObject<FocusMessage>(
                        message.Substring(focusPrefix.Length)
                    );
                    if (
                        !_visible
                        || !_focusBound
                        || focus == null
                        || focus.Document != _focusDocument
                        || focus.Revision < 1
                        || focus.Request < 0
                    )
                        return;
                    if (focus.Kind != "return")
                    {
                        FocusMessageReceived?.Invoke(focus);
                        return;
                    }
                    source = focus.Ticket;
                    message = "xso-keyboard-ime return " + focus.Index;
                }
                catch (Newtonsoft.Json.JsonException exception)
                {
                    HostLog.Warn("focus request was rejected: " + exception.Message);
                    return;
                }
            }
            const string chatboxPrefix = "xso-keyboard-ime chatbox ";
            if (message.StartsWith(chatboxPrefix, StringComparison.Ordinal))
            {
                try
                {
                    ChatboxState state = Newtonsoft.Json.JsonConvert.DeserializeObject<ChatboxState>(
                        message.Substring(chatboxPrefix.Length)
                    );
                    if (state != null)
                        ChatboxChanged?.Invoke(state);
                }
                catch (Newtonsoft.Json.JsonException exception)
                {
                    HostLog.Warn("ChatBox state was rejected: " + exception.Message);
                }
                return;
            }
            if (message.StartsWith(ComposerReturnFeedback.MessagePrefix, StringComparison.Ordinal))
            {
                try
                {
                    var request = Newtonsoft.Json.JsonConvert.DeserializeObject<ComposerReturnFeedback.Request>(
                        message.Substring(ComposerReturnFeedback.MessagePrefix.Length)
                    );
                    object feedback = ComposerReturnFeedback.Play(request);
                    Call(
                        _listening,
                        "Return sound",
                        CallVoid("playReturnSound", JavaScriptJson.Serialize(feedback)),
                        outcome =>
                        {
                            if (outcome != "ok")
                                HostLog.Warn("Return sound failed: " + outcome);
                        }
                    );
                }
                catch (Exception exception)
                {
                    HostLog.Warn("Return feedback failed: " + exception.Message);
                }
                return;
            }
            int returnId;
            if (PageMessages.TryReadReturnRequest(message, out returnId))
            {
                if (!source.HasValue)
                    return;
                HostLog.Info("Return request " + returnId + " received");
                bool taken = ComposerReturnInput.TryAccept(source.Value);
                HostLog.Info("Return request " + returnId + (taken ? " -> IME" : " -> page"));
                IWebView page = sender as IWebView;
                if (page != null && ReferenceEquals(page, _listening))
                    page.ExecuteJavaScript(
                        Wrap(
                            "resolveReturn",
                            "if(a.ownsFocus("
                                + JavaScriptJson.Serialize(
                                    new
                                    {
                                        document = source.Value.Document,
                                        revision = source.Value.Revision,
                                        request = source.Value.Request,
                                    }
                                )
                                + "))a.resolveReturn("
                                + returnId
                                + ","
                                + (taken ? "true" : "false")
                                + ");return 'ok'"
                        ),
                        outcome =>
                        {
                            if (outcome != "ok")
                                HostLog.Warn("Return routing failed: " + outcome);
                        }
                    );
                return;
            }
            if (message.StartsWith(CandidateScrollArea.MessagePrefix, StringComparison.Ordinal))
            {
                try
                {
                    _scrollArea = CandidateScrollArea.Read(message.Substring(CandidateScrollArea.MessagePrefix.Length));
                }
                catch (Newtonsoft.Json.JsonException exception)
                {
                    _scrollArea = null;
                    HostLog.Warn("candidate area was rejected: " + exception.Message);
                }
                return;
            }
            if (message == PageScript.ReadyMessage)
            {
                _confirmed = true;
                HostLog.Info("page message: " + message);
                Attach();
            }
        }
    }
}
