/** Actions the candidate bar sends the plugin. The plugin reads this line; see BarAction. */
export type BarAction =
  { kind: 'ready' } | { kind: 'pick'; index: number } | { kind: 'turn'; delta: number }

const Prefix = 'xso-keyboard-ime'

function format(action: BarAction): string {
  switch (action.kind) {
    case 'ready':
      return `${Prefix} ready`
    case 'pick':
      return `${Prefix} pick ${action.index}`
    case 'turn':
      return `${Prefix} turn ${action.delta}`
  }
}

/**
 * Page to plugin messages. Vuplex hands each one to C# as an IWebView.MessageEmitted
 * event, so the plugin never polls. See ADR 0005.
 */
export function postToPlugin(action: BarAction): boolean {
  const host = window.vuplex
  if (host == null) {
    return false
  }

  host.postMessage(format(action))
  return true
}
