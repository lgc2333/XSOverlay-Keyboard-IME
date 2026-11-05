import { insertText } from './composer'
import { postToPlugin } from './native-bridge'

/**
 * Entry point of the injected script. The keyboard page imports it for its side
 * effect: publish the plugin API, then announce that the page is patched.
 */
function install(): void {
  window.__xsoKeyboardIme = { insertText }

  if (!postToPlugin({ kind: 'ready' })) {
    console.warn(
      '[xso-ime] the Vuplex message host is missing; the plugin cannot see this page',
    )
  }
}

install()
