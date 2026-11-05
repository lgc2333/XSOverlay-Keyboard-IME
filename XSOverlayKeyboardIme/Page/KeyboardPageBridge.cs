using System;
using System.IO;
using System.Reflection;
using System.Text;
using Vuplex.WebView;
using XSOverlayKeyboardIme.Diagnostics;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>
    /// Injects the bundled page script and writes committed text into the composer with
    /// it. See ADR 0004 and ADR 0008. Every method here runs on Unity's main thread.
    /// </summary>
    internal static class KeyboardPageBridge
    {
        private const string ScriptResource = "keyboard-ime.js";
        private const string InsertText = "window.__xsoKeyboardIme && window.__xsoKeyboardIme.insertText";
        private const string Missing = "missing";

        private static string _script;
        private static bool _scriptLoaded;
        private static IWebView _attached;

        /// <summary>Injects once per page and starts listening to what the page reports.</summary>
        internal static void Attach(IWebView webView)
        {
            if (webView == null || webView.IsDisposed || ReferenceEquals(webView, _attached))
            {
                return;
            }

            _attached = webView;
            webView.MessageEmitted += (sender, args) => HostLog.Info("page message: " + args.Value);
            Inject(webView);
        }

        internal static void WriteBack(IWebView webView, string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            if (webView == null || webView.IsDisposed)
            {
                HostLog.Warn("writeback skipped: the keyboard page is gone");
                return;
            }

            Attach(webView);
            Run(webView, text, canReinject: true);
        }

        private static void Inject(IWebView webView)
        {
            string script = LoadScript();
            if (script.Length == 0)
            {
                return;
            }

            webView.ExecuteJavaScript(script, outcome => HostLog.Info("page script injected -> " + outcome));
        }

        private static void Run(IWebView webView, string text, bool canReinject)
        {
            string call = "(" + InsertText + ")(" + Quote(text) + ") ?? '" + Missing + "'";
            bool injected = ReferenceEquals(webView, _attached);
            string script = injected ? call : LoadScript() + ";\n" + call;
            _attached = webView;

            webView.ExecuteJavaScript(
                script,
                outcome =>
                {
                    HostLog.Info("writeback '" + text + "' -> " + outcome);
                    if (outcome == null || !outcome.Contains(Missing) || !canReinject)
                    {
                        return;
                    }

                    // The page reloaded since the last injection, so inject once more.
                    _attached = null;
                    Run(webView, text, canReinject: false);
                }
            );
        }

        private static string LoadScript()
        {
            if (_scriptLoaded)
            {
                return _script;
            }

            _scriptLoaded = true;
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(ScriptResource))
            {
                if (stream == null)
                {
                    HostLog.Error("page script is not embedded; build the web project before the plugin");
                    return string.Empty;
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    _script = reader.ReadToEnd();
                }
            }

            return _script;
        }

        /// <summary>Wraps text as a JavaScript string literal, so quotes and newlines survive.</summary>
        private static string Quote(string text)
        {
            var quoted = new StringBuilder(text.Length + 2);
            quoted.Append('"');
            foreach (char character in text)
            {
                switch (character)
                {
                    case '"':
                        quoted.Append("\\\"");
                        break;
                    case '\\':
                        quoted.Append("\\\\");
                        break;
                    case '\n':
                        quoted.Append("\\n");
                        break;
                    case '\r':
                        quoted.Append("\\r");
                        break;
                    case '\t':
                        quoted.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            quoted.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            quoted.Append(character);
                        }

                        break;
                }
            }

            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
