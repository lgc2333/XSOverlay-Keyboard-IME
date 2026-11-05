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
        private const string InsertText = "window.__xsoKeyboardIme && window.__xsoKeyboardIme.insertText";
        private const string ShowComposition = "window.__xsoKeyboardIme && window.__xsoKeyboardIme.showComposition";

        private EventHandler<EventArgs<string>> _messageHandler;
        private IWebView _listening;
        private IWebView _injected;

        /// <summary>Injects the bundle and starts reading what the page reports.</summary>
        internal void Attach(IWebView webView)
        {
            if (webView == null || webView.IsDisposed)
            {
                return;
            }

            if (!ReferenceEquals(webView, _injected))
            {
                _injected = webView;
                PageScript.Inject(webView, outcome => HostLog.Info("page script injected -> " + outcome));
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
                "(" + InsertText + ")(" + JavaScriptString.Literal(text) + ") ?? '" + PageScript.Missing + "'",
                outcome => HostLog.Info("writeback '" + text + "' -> " + outcome)
            );
        }

        /// <summary>Shows the composing string, the candidates and the idle state.</summary>
        internal void Show(IWebView webView, CompositionView view)
        {
            Attach(webView);
            Call(
                webView,
                "composition",
                "(" + ShowComposition + ")(" + CompositionPayload.Build(view) + ") ?? '" + PageScript.Missing + "'",
                outcome =>
                {
                    if (outcome == null || outcome.Contains(PageScript.Missing))
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
            HostLog.Info("page message: " + args.Value);
        }
    }
}
