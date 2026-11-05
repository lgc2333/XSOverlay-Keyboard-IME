using System;
using Vuplex.WebView;
using XSOverlayKeyboardIme.Composition;
using XSOverlayKeyboardIme.Diagnostics;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>
    /// The keyboard page as this plugin uses it: inject the bundle, write committed text
    /// into the composer and show the composition. Every member runs on Unity's main
    /// thread. See ADR 0004 and ADR 0008.
    /// </summary>
    internal sealed class KeyboardPage
    {
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

        private EventHandler<EventArgs<string>> _messageHandler;
        private IWebView _listening;
        private IWebView _injected;
        private bool _confirmed;

        /// <summary>Injects the bundle and starts reading what the page reports.</summary>
        internal void Attach(IWebView webView)
        {
            if (webView == null || webView.IsDisposed)
            {
                return;
            }

            // The page confirms the bundle with its ready message. Until that arrives the
            // injection may have landed on an unloaded page, so it is worth repeating.
            if (!ReferenceEquals(webView, _injected) || !_confirmed)
            {
                _injected = webView;
                bool repeat = _confirmed == false && !ReferenceEquals(webView, _listening);
                PageScript.Inject(
                    webView,
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

            Listen(webView);
        }

        /// <summary>Writes committed text into the composer. See ADR 0008.</summary>
        internal void WriteBack(IWebView webView, string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            Attach(webView);
            Call(
                webView,
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

        /// <summary>Shows the composing string, the candidates and the idle state.</summary>
        internal void Show(IWebView webView, CompositionView view)
        {
            Attach(webView);
            Call(
                webView,
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
        private void Call(
            IWebView webView,
            string label,
            string expression,
            Action<string> onOutcome,
            bool canInject = true
        )
        {
            if (webView == null || webView.IsDisposed)
            {
                HostLog.Warn("the keyboard page is gone; dropping the " + label);
                return;
            }

            bool injected = ReferenceEquals(webView, _injected);
            string script = injected ? expression : PageScript.Source() + ";\n" + expression;
            _injected = webView;

            webView.ExecuteJavaScript(
                script,
                outcome =>
                {
                    if (outcome != null && outcome.Contains(PageScript.Missing) && canInject)
                    {
                        // The page reloaded since the last injection, so inject once more.
                        _injected = null;
                        Attach(webView);
                        Call(webView, label, expression, onOutcome, canInject: false);
                        return;
                    }

                    onOutcome(outcome);
                }
            );
        }

        private void Listen(IWebView webView)
        {
            if (ReferenceEquals(webView, _listening))
            {
                return;
            }

            IWebView previous = _listening;
            _listening = webView;
            _messageHandler ??= OnMessage;
            if (previous != null && !previous.IsDisposed)
            {
                previous.MessageEmitted -= _messageHandler;
            }

            webView.MessageEmitted += _messageHandler;
        }

        /// <summary>The page reports that the bundle runs; nothing else travels this way.</summary>
        private void OnMessage(object sender, EventArgs<string> args)
        {
            _confirmed = true;
            HostLog.Info("page message: " + args.Value);
        }
    }
}
