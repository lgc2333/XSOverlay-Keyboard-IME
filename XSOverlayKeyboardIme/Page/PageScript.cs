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

        /// <summary>The page asks the plugin to press the IME's language bar button.</summary>
        internal const string SwitchMessage = "xso-keyboard-ime switch";

        /// <summary>The page asks the plugin to cancel the composition the IME holds.</summary>
        internal const string CancelMessage = "xso-keyboard-ime cancel";

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
