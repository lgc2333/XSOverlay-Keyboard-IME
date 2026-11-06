const Ready = 'xso-keyboard-ime ready'
const Switch = 'xso-keyboard-ime switch'
const Cancel = 'xso-keyboard-ime cancel'

/**
 * Tells the plugin that the page runs the injected script. Vuplex hands the message to
 * C# as an IWebView.MessageEmitted event, so the plugin never polls. See ADR 0005.
 */
export function announceReady(): boolean {
  return post(Ready)
}

/**
 * Asks the plugin to press the IME's own input-mode button, which is how the switch
 * changes 中 and 英 without the plugin writing a mode. See ADR 0028.
 */
export function requestSwitch(): boolean {
  return post(Switch)
}

/**
 * Asks the plugin to cancel the composition the IME holds, because the composer no
 * longer holds the text it was composing into. See ADR 0027.
 */
export function requestCancel(): boolean {
  return post(Cancel)
}

function post(message: string): boolean {
  const host = window.vuplex
  if (host == null) {
    return false
  }

  host.postMessage(message)
  return true
}
