import { createCandidateBar } from './candidate-bar'
import { routeComposerReturn } from './composer-return'
import {
  clearPreedit,
  insertText,
  keepComposerFocus,
  onCompositionCancel,
  setImeComposing,
  showPreedit,
} from './composer'
import type {
  ComposerStateView,
  CompositionView,
  SwitchIconView,
} from './ime-view'
import { announceReady, requestCancel } from './native-bridge'
import { createSwitchKey } from './switch-key'

/**
 * Entry point of the injected script. The keyboard page imports it for its side
 * effect: publish the plugin API, then announce that the page is patched. Injecting
 * twice is harmless, so the script guards the install instead of each call.
 */
function install(): void {
  const bar = createCandidateBar()
  const switchKey = createSwitchKey()
  const returnRouting = routeComposerReturn()
  // Cancelled codes may still have queued views; the host's idle view ends that stream.
  let cancelledComposition = false
  keepComposerFocus((active) => switchKey.setActive(active))
  onCompositionCancel(() => {
    cancelledComposition = true
    bar.render(null)
    requestCancel()
  })

  window.__xsoKeyboardIme = {
    resolveReturn: returnRouting.resolve,
    playReturnSound: returnRouting.sound,
    insertText,
    showComposition: (view: CompositionView) => {
      if (cancelledComposition) {
        if (view.composing) return
        cancelledComposition = false
      }
      setImeComposing(view.composing)
      if (view.preedit.length > 0) {
        showPreedit(view.preedit, view.selectionStart, view.selectionEnd)
      } else {
        clearPreedit()
      }

      bar.render(cancelledComposition ? null : view)
    },
    showSwitchIcon: (view: SwitchIconView | null) => {
      switchKey.show(view)
    },
    showComposerState: (view: ComposerStateView) => {
      bar.showState(view)
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
