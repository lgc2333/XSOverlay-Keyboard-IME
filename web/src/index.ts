import { nativeKeyOperations } from './native-key-operation'
import { createCandidateBar } from './candidate-bar'
import { observeChatbox } from './chatbox'
import { installComposerSubmit } from './composer-submit'
import { routeComposerReturn } from './composer-return'
import {
  clearPreedit,
  committedComposerText,
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
import { installComposerAdmission } from './composer-admission'
import { hasComposerFocus, releaseComposerFocus } from './composer-focus'

/**
 * Entry point of the injected script. The keyboard page imports it for its side
 * effect: publish the plugin API, then announce that the page is patched. Injecting
 * twice is harmless, so the script guards the install instead of each call.
 */
function install(): void {
  const bar = createCandidateBar()
  const chatbox = observeChatbox(committedComposerText)
  const switchKey = createSwitchKey(chatbox.beginSwitch)
  const submit = installComposerSubmit()
  const returnRouting = routeComposerReturn(submit.defer)
  const refreshChatbox = chatbox.refresh
  keepComposerFocus((active) => {
    if (!active) {
      setImeComposing(false)
      clearPreedit()
      bar.render(null)
    }
    switchKey.setActive(active)
    refreshChatbox()
  })
  onCompositionCancel(() => {
    bar.render(null)
    requestCancel()
  })
  const nativeOperations = nativeKeyOperations()
  const admission = installComposerAdmission((restored) => {
    nativeOperations.cancelNativeKey()
    returnRouting.cancel()
    setImeComposing(false)
    clearPreedit()
    bar.render(null)
    switchKey.setActive(false)
    submit.clear(restored)
    releaseComposerFocus()
  }, chatbox.beginEditing)

  window.__xsoKeyboardIme = {
    ...admission,
    ...nativeOperations,
    refreshChatbox,
    setKeyboardFormat: switchKey.setKeyboardFormat,
    resolveReturn: returnRouting.resolve,
    playReturnSound: returnRouting.sound,
    insertText: (text: string) => hasComposerFocus() ? insertText(text) : 'focus-lost',
    showComposition: (view: CompositionView) => {
      if (!hasComposerFocus()) {
        setImeComposing(false)
        clearPreedit()
        bar.render(null)
        return
      }
      setImeComposing(view.composing)
      if (view.preedit.length > 0) {
        showPreedit(view.preedit, view.selectionStart, view.selectionEnd)
      } else {
        clearPreedit()
      }

      bar.render(view)
    },
    showSwitchIcon: (view: SwitchIconView | null) => {
      switchKey.show(view)
      chatbox.endSwitch()
    },
    showComposerState: (view: ComposerStateView) => {
      bar.showState(view)
      chatbox.endSwitch()
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
