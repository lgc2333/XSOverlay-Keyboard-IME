using System;
using System.Collections.Generic;
using Vuplex.WebView;

namespace XSOverlayKeyboardIme.Page
{
    /// <summary>Owns native edge recipients and the page completion handshake.</summary>
    internal sealed class NativeKeyDelivery
    {
        private readonly Func<IWebView> _currentPage;
        private long _nativeOperationId;
        private Action<bool> _nativeCompleted;
        private string _nativeToken;
        private readonly Dictionary<string, IWebView> NativeRecipients = new Dictionary<string, IWebView>();

        internal NativeKeyDelivery(Func<IWebView> currentPage)
        {
            _currentPage = currentPage;
        }

        internal void Acknowledge(string token, bool success)
        {
            if (token == _nativeToken)
                Finish(success);
        }

        internal void ReleaseKeys()
        {
            foreach (var held in NativeRecipients)
            {
                if (!held.Value.IsDisposed && held.Value is IWithKeyDownAndUp keys)
                    try
                    {
                        keys.KeyUp(held.Key, KeyModifier.None);
                    }
                    catch (Exception) { }
            }
            NativeRecipients.Clear();
        }

        internal void Send(string key, bool up, Action<bool> completed)
        {
            IWebView page;
            if (up)
            {
                if (!NativeRecipients.TryGetValue(key, out page))
                {
                    completed(false);
                    return;
                }
                NativeRecipients.Remove(key);
            }
            else if (!NativeRecipients.TryGetValue(key, out page))
            {
                page = _currentPage();
                if (page != null)
                    NativeRecipients[key] = page;
            }
            if (page == null || page.IsDisposed || !(page is IWithKeyDownAndUp keys))
            {
                completed(false);
                return;
            }
            string token = (++_nativeOperationId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _nativeToken = token;
            _nativeCompleted = completed;
            try
            {
                page.ExecuteJavaScript(
                    PageScript.CallReturning(
                        "armNativeKey",
                        JavaScriptJson.Serialize(token)
                            + ","
                            + JavaScriptJson.Serialize(key)
                            + ","
                            + (up ? "true" : "false")
                    ),
                    result =>
                    {
                        if (_nativeToken != token)
                            return;
                        if (result != "armed")
                        {
                            Finish(false);
                            return;
                        }
                        try
                        {
                            if (up)
                                keys.KeyUp(key, NativeModifiers());
                            else
                                keys.KeyDown(key, NativeModifiers());
                            page.ExecuteJavaScript(
                                PageScript.CallVoid("finishNativeKey", JavaScriptJson.Serialize(token)),
                                outcome =>
                                {
                                    if (_nativeToken == token && outcome != "ok")
                                        Finish(false);
                                }
                            );
                        }
                        catch (Exception)
                        {
                            Finish(false);
                        }
                    }
                );
            }
            catch (Exception)
            {
                Finish(false);
            }
        }

        private KeyModifier NativeModifiers()
        {
            KeyModifier modifiers = KeyModifier.None;
            if (NativeRecipients.ContainsKey("Shift"))
                modifiers |= KeyModifier.Shift;
            if (NativeRecipients.ContainsKey("Control"))
                modifiers |= KeyModifier.Control;
            if (NativeRecipients.ContainsKey("Alt"))
                modifiers |= KeyModifier.Alt;
            if (NativeRecipients.ContainsKey("Meta"))
                modifiers |= KeyModifier.Meta;
            return modifiers;
        }

        internal void Finish(bool success)
        {
            Action<bool> completed = _nativeCompleted;
            _nativeCompleted = null;
            _nativeToken = null;
            completed?.Invoke(success);
        }

        internal void Cancel()
        {
            Finish(false);
            IWebView page = _currentPage();
            if (page != null && !page.IsDisposed)
                page.ExecuteJavaScript(PageScript.CallVoid("cancelNativeKey", string.Empty), _ => { });
        }
    }
}
