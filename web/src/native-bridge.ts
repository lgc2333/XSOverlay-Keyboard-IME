/**
 * Page to plugin messages. Vuplex hands each one to C# as an IWebView.MessageEmitted
 * event, so the plugin never polls. See ADR 0005.
 */
export interface KeyboardPageEvent {
  kind: 'ready'
}

/** Reports whether the message left the page. */
export function postToPlugin(event: KeyboardPageEvent): boolean {
  const host = window.vuplex
  if (host == null) {
    return false
  }

  host.postMessage(JSON.stringify({ source: 'xso-keyboard-ime', kind: event.kind }))
  return true
}
