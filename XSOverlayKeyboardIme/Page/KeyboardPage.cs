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

        /// <summary>What the bar shows right now, replayed onto a page that appears later.</summary>
        private static CompositionView _view = CompositionView.Idle(string.Empty);

        /// <summary>Shows a composition or the idle bar, and remembers it for a later page.</summary>
        internal static void Show(CompositionView view)
        {
            _view = view;
            Attach();
        }

        /// <summary>
        /// Puts the bundle and the current bar on the keyboard page. Called when the
        /// keyboard opens and once at start-up, for a page that already exists.
        /// </summary>
        internal static void Attach()
        {
            IWebView page = EnsureInjected();
            if (page == null)
            {
                return;
            }

            ShowBar(page, _view);
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
                CallReturning("insertText", JavaScriptString.Literal(text)),
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
            Inject(page);
            Listen(page);
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

        /// <summary>The page reports that the bundle runs; nothing else travels this way.</summary>
        private static void OnMessage(object sender, EventArgs<string> args)
        {
            _confirmed = true;
            HostLog.Info("page message: " + args.Value);
        }
    }
}
