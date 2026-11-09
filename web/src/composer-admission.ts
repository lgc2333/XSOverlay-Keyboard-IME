import { findComposer, hasComposerFocus } from './composer-focus'
import type { FocusIdentity } from './native-bridge'
import { reportFocus, useFocusIdentity } from './native-bridge'

export interface FocusUpdate extends FocusIdentity {
  kind: string
}

/** Admission follows page-event arrival (ADR 0060); native browser input stays untouched. */
export function installComposerAdmission(clear: () => void) {
  // Keyboard pages may use a non-secure local origin, where randomUUID is unavailable.
  const focusDocument = Array.from(
    crypto.getRandomValues(new Uint8Array(16)),
    (value) => value.toString(16).padStart(2, '0'),
  ).join('')
  let generation = 0
  let documentId: string | null = null
  let revision = 0
  let sequence = 0
  let request = 0
  let phase: 'closed' | 'cleared' | 'open' | 'pending' | 'held' = 'closed'
  let tab: FocusIdentity | null = null

  function identity(): FocusIdentity {
    return { document: documentId!, revision, request }
  }
  function bindFocus(
    id: string,
    nextGeneration: number,
    expectedDocument: string,
  ): boolean {
    if (expectedDocument !== focusDocument || nextGeneration <= generation) return false
    generation = nextGeneration
    documentId = id
    revision = 0
    request = 0
    phase = 'closed'
    tab = null
    useFocusIdentity(() => (phase === 'held' ? identity() : null))
    return true
  }
  function applyFocus(update: FocusUpdate): void {
    if (update.document !== documentId || update.revision < revision) return
    if (update.kind === 'clear') {
      if (update.revision === revision) {
        if (phase === 'cleared') reportFocus('cleared', identity())
        return
      }
      revision = update.revision
      request = 0
      phase = 'cleared'
      tab = null
      const captured = identity()
      clear()
      if (
        phase === 'cleared' &&
        captured.document === documentId &&
        captured.revision === revision
      ) {
        reportFocus('cleared', captured)
      }
    } else if (update.revision === revision) {
      if (update.kind === 'open' && phase === 'cleared') { phase = 'open'
}
      else if (
        update.kind === 'held' &&
        phase === 'pending' &&
        update.request === request
      ) {
        phase = 'held'
}
    }
  }
  function unbindFocus(expected: string): void {
    if (documentId !== expected) return
    phase = 'closed'
    documentId = null
    tab = null
    clear()
  }
  function requestFocus(): void {
    if (phase !== 'open' || documentId == null) return
    request = ++sequence
    phase = 'pending'
    if (!reportFocus('request', identity())) phase = 'closed'
  }
  function cancel(): void {
    if (phase !== 'pending' && phase !== 'held') return
    const captured = identity()
    phase = 'closed'
    tab = null
    reportFocus('cancel-focus', captured)
  }

  document.addEventListener(
    'pointerdown',
    (event) => {
      if (
        event.isTrusted &&
        event.isPrimary &&
        event.button === 0 &&
        event.target === findComposer()
      ) {
        requestFocus()
}
    },
    true,
  )
  document.addEventListener(
    'keydown',
    (event) => {
      if (event.isTrusted && event.key === 'Tab' && !event.repeat)
        tab = phase === 'open' ? identity() : null
    },
    true,
  )
  document.addEventListener(
    'keyup',
    (event) => {
      if (!event.isTrusted || event.key !== 'Tab') return
      const down = tab
      tab = null
      if (
        down?.document === documentId &&
        down?.revision === revision &&
        event.target === findComposer() &&
        hasComposerFocus()
      ) {
        requestFocus()
}
    },
    true,
  )
  document.addEventListener(
    'focusout',
    () => {
      const captured = identity()
      queueMicrotask(() => {
        if (
          captured.document === documentId &&
          captured.revision === revision &&
          captured.request === request &&
          !hasComposerFocus()
        ) {
          cancel()
}
      })
    },
    true,
  )
  window.addEventListener('blur', cancel)
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) cancel()
  })

  function ownsFocus(expected: FocusIdentity): boolean {
    return (
      phase === 'held' &&
      documentId === expected.document &&
      revision === expected.revision &&
      request === expected.request
    )
  }
  return { focusDocument, bindFocus, applyFocus, unbindFocus, ownsFocus }
}
