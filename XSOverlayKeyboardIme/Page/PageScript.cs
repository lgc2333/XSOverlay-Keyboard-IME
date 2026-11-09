using System;
using System.IO;
using System.Text;
using Vuplex.WebView;
using XSOverlayKeyboardIme.Diagnostics;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>The bundled page script, read once from the plugin assembly. See ADR 0004.</summary>
    internal static class PageScript
    {
        internal const string Missing = "missing";

        internal const string ReadyMessage = "xso-keyboard-ime ready";

        private const string ResourceName = "keyboard-ime.js";

        private static string _source;
        private static bool _loaded;

        /// <summary>Empty only when the bundle is not embedded, which the build target prevents.</summary>
        internal static string Source()
        {
            if (_loaded)
            {
                return _source;
            }

            _loaded = true;
            using (Stream stream = typeof(PageScript).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    HostLog.Error("page script is not embedded; build the web project before the plugin");
                    return _source = string.Empty;
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    _source = reader.ReadToEnd();
                }
            }

            return _source;
        }

        internal static string CallReturning(string method, string arguments)
        {
            return Wrap(method, "return a." + method + "(" + arguments + ")");
        }

        /// <summary>Calls a page function that answers nothing; the sentinel means the bundle is gone.</summary>
        internal static string CallVoid(string method, string arguments)
        {
            return Wrap(method, "try{a." + method + "(" + arguments + ");return 'ok'}catch(e){return 'error:' + e}");
        }

        internal static string Wrap(string method, string body)
        {
            return "(function(){var a=window.__xsoKeyboardIme;if(!a||!a."
                + method
                + ")return '"
                + PageScript.Missing
                + "';"
                + body
                + "})()";
        }

        /// <summary>Puts the bundle into a page that has not run it yet.</summary>
        internal static void Inject(IWebView webView, Action<string> onOutcome)
        {
            string source = Source();
            if (source.Length == 0)
            {
                return;
            }

            webView.ExecuteJavaScript(source, onOutcome);
        }
    }
}
