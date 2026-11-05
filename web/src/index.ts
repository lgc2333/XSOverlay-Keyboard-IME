import { createCandidateBar } from './candidate-bar'
import { clearPreedit, insertText, showPreedit } from './composer'
import type { CompositionView } from './ime-view'
import { announceReady } from './native-bridge'

/**
 * Entry point of the injected script. The keyboard page imports it for its side
 * effect: publish the plugin API, then announce that the page is patched. Injecting
 * twice is harmless, so the script guards the install instead of each call.
 */
function install(): void {
  const bar = createCandidateBar()

  window.__xsoKeyboardIme = {
    insertText,
    showComposition: (view: CompositionView) => {
      if (view.preedit.length > 0) {
        showPreedit(view.preedit)
      } else {
        clearPreedit()
      }

      bar.render(view)
    },
  }

  if (!announceReady()) {
    console.warn(
      '[xso-ime] the Vuplex message host is missing; the plugin cannot see this page',
    )
  }
}

if (window.__xsoKeyboardIme == null) {
  install()
}
