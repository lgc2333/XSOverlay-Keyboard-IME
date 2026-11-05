const Ready = 'xso-keyboard-ime ready'

/**
 * Tells the plugin that the page runs the injected script. Vuplex hands the message to
 * C# as an IWebView.MessageEmitted event, so the plugin never polls. See ADR 0005.
 */
export function announceReady(): boolean {
  const host = window.vuplex
  if (host == null) {
    return false
  }

  host.postMessage(Ready)
  return true
}
