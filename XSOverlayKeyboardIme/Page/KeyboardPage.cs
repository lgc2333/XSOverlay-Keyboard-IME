using System;
using Vuplex.WebView;
using XSOverlayKeyboardIme.Diagnostics;
using XSOverlayKeyboardIme.Game;
using XSOverlayKeyboardIme.Ime;

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

        /// <summary>Raised when the page's switch button is pressed. Set by the root.</summary>
        internal static Action SwitchRequested;

        /// <summary>Raised when the page reports that the composition no longer fits. Set by the root.</summary>
        internal static Action CancelRequested;

        /// <summary>Raised with the index of the IME the page asked for. Set by the root.</summary>
        internal static Action<int> ProfileRequested;

        /// <summary>Raised with true when the composer takes the focus and false when it drops it.</summary>
        internal static Action<bool> ComposerActivationChanged;

        /// <summary>What the bar shows right now, replayed onto a page that appears later.</summary>
        private static CompositionView _view = CompositionView.Idle();

        /// <summary>Whether the composer holds the focus, and which IME it composes with. See ADR 0035.</summary>
        private static ComposerState _composerState = ComposerState.Hidden;

        /// <summary>The IME's input-mode icon, replayed onto a page that appears later.</summary>
        private static LanguageBarIcon _switchIcon;

        /// <summary>Shows the IME's input-mode icon, the one the page's switch button draws.</summary>
        internal static void ShowSwitchIcon(LanguageBarIcon icon)
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

            ShowBar(page, _view);
            PushSwitchIcon(page);
            PushComposerState(page);
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
            IWebView page = KeyboardOverlayScope.FindPage();
            if (page == null || page.IsDisposed)
            {
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

        private static void PushSwitchIcon(IWebView page)
        {
            LanguageBarIcon icon = _switchIcon;

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
            _listening = page;
            _messageHandler ??= OnMessage;
            if (previous != null && !previous.IsDisposed)
            {
                previous.MessageEmitted -= _messageHandler;
            }

            page.MessageEmitted += _messageHandler;
        }

        private static void OnMessage(object sender, EventArgs<string> args)
        {
            if (!ReferenceEquals(sender, _listening))
                return;
            string message = args.Value;
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
                bool taken = ComposerReturnInput.TryAccept();
                IWebView page = sender as IWebView;
                if (page != null && ReferenceEquals(page, _listening))
                    Call(
                        page,
                        "Return routing",
                        CallVoid(
                            "resolveReturn",
                            JavaScriptJson.Serialize(returnId) + "," + (taken ? "true" : "false")
                        ),
                        outcome =>
                        {
                            if (outcome != "ok")
                                HostLog.Warn("Return routing failed: " + outcome);
                        }
                    );
                return;
            }
            if (message == PageScript.SwitchMessage)
            {
                OnSwitchRequested();
                return;
            }

            if (message == PageScript.CancelMessage)
            {
                OnCancelRequested();
                return;
            }

            int profile;
            if (PageMessages.TryReadProfileIndex(message, out profile))
            {
                OnProfileRequested(profile);
                return;
            }

            if (message == PageScript.ComposerActiveMessage)
            {
                OnComposerActivationChanged(true);
                return;
            }

            if (message == PageScript.ComposerInactiveMessage)
            {
                OnComposerActivationChanged(false);
                return;
            }

            if (message == PageScript.ReadyMessage)
            {
                _confirmed = true;
                HostLog.Info("page message: " + message);
                Attach();
            }
        }

        /// <summary>The page's switch was pressed; the root presses the IME's button.</summary>
        private static void OnSwitchRequested()
        {
            Action switchRequested = SwitchRequested;
            if (switchRequested != null)
            {
                switchRequested();
            }
        }

        /// <summary>The composer took or dropped the focus; the root shows or hides its state.</summary>
        private static void OnComposerActivationChanged(bool active)
        {
            Action<bool> changed = ComposerActivationChanged;
            if (changed != null)
            {
                changed(active);
            }
        }

        /// <summary>The page asked for another IME; the root switches the thread's profile.</summary>
        private static void OnProfileRequested(int index)
        {
            Action<int> profileRequested = ProfileRequested;
            if (profileRequested != null)
            {
                profileRequested(index);
            }
        }

        /// <summary>The page reports that the composition no longer fits; the root cancels it.</summary>
        private static void OnCancelRequested()
        {
            Action cancelRequested = CancelRequested;
            if (cancelRequested != null)
            {
                cancelRequested();
            }
        }
    }
}
