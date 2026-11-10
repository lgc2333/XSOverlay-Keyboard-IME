import { findComposer, hasComposerFocus } from './composer-focus'
import { reportChatbox } from './native-bridge'
import { createChatboxOverflow } from './chatbox-overflow'

/** Observe native toolbar updates too: speech and clear do not emit input events. */
export function observeChatbox(readText: () => string) {
  let previous = ''
  let scheduled = false
  let revision = 0
  let completed = false
  const showOverflow = createChatboxOverflow()

  function sync(): void {
    scheduled = false
    const composer = findComposer()
    const toggle = document.getElementById('keyboard-toolbar-button-vrc')
    const enabled = toggle?.getAttribute('aria-pressed') === 'true' && !toggle.hidden
    const text = readText()
    showOverflow(enabled && text.length > 144)
    const state = {
      revision,
      active: !completed && hasComposerFocus() && enabled,
      text,
      submitting: composer?.getAttribute('aria-busy') === 'true',
    }
    const json = JSON.stringify(state)
    if (json !== previous && reportChatbox(state)) previous = json
  }

  function refresh(): void {
    if (scheduled) return
    scheduled = true
    // IME writeback clears its inline codes before inserting the committed text.
    queueMicrotask(sync)
  }

  document.addEventListener('input', refresh)
  document.addEventListener('focusin', refresh)
  document.addEventListener('focusout', refresh)
  window.addEventListener('blur', refresh)
  window.addEventListener('focus', refresh)
  document.addEventListener('visibilitychange', refresh)
  const observer = new MutationObserver(refresh)
  observer.observe(document.documentElement, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['aria-pressed', 'aria-busy', 'placeholder', 'hidden'],
  })
  refresh()
  return {
    beginEditing: () => {
      completed = false
      refresh()
    },
    refresh: (nextRevision?: number, success = false) => {
      if (nextRevision != null) {
        if (nextRevision < revision) return
        revision = nextRevision
        previous = ''
        // Success precedes native blur. A fresh snapshot must not reopen this edit.
        if (success) completed = true
      }
      refresh()
    },
  }
}
