import { findComposer, hasComposerFocus, observeComposerFocus } from './composer-focus'
import { createPreeditDisplay } from './preedit-display'

export type WritebackOutcome = 'inserted' | 'empty' | 'focus-lost' | 'rejected'

const display = createPreeditDisplay()
let imeComposing = false
let composerBasis: HTMLInputElement | null = null
let expectedValue = ''
let expectedStart = 0
let expectedEnd = 0
let writing = false
let sawNativeInput = false
let displayedComposer: HTMLInputElement | null = null
let cancelComposition: (() => void) | null = null

/** Reports real focus loss immediately; results never restore an earlier display. */
export function keepComposerFocus(onActiveChange: (active: boolean) => void): void {
  observeComposerFocus((active) => {
    if (!active) cancel()
    onActiveChange(active)
  }, () => cancel())
  document.addEventListener('focusout', (event) => {
    if (event.target === composerBasis || event.target === displayedComposer) cancel()
  }, true)
  const observer = new MutationObserver(() => {
    if ((composerBasis != null && (!composerBasis.isConnected || findComposer() !== composerBasis)) || (displayedComposer != null && (!displayedComposer.isConnected || findComposer() !== displayedComposer))) cancel()
  })
  observer.observe(document.documentElement, { childList: true, subtree: true })
}

/** Watch committed text and native selection while the host composes. */
export function setImeComposing(value: boolean): void {
  if (value === imeComposing) return
  imeComposing = value
  if (value) {
    const composer = currentComposer()
    if (composer == null) {
      imeComposing = false
      return
    }
    remember(composer)
    document.addEventListener('selectionchange', checkCaret, true)
    document.addEventListener('input', checkCaret, true)
    document.addEventListener('pointerup', checkCaretLater, true)
  } else {
    unwatch()
  }
}

export function onCompositionCancel(callback: () => void): void {
  cancelComposition = callback
}

export { findComposer } from './composer-focus'

export function committedComposerText(): string {
  return findComposer()?.value ?? ''
}

/** Native insertion preserves undo; success acknowledges the actual resulting document. */
export function insertText(text: string): WritebackOutcome {
  if (text === '') return 'empty'
  const composer = currentComposer()
  if (composer == null) return 'focus-lost'
  if (writing) return 'rejected'
  checkCaret()
  const value = composer.value
  const start = composer.selectionStart ?? value.length
  const end = composer.selectionEnd ?? start
  const result = value.slice(0, start) + text + value.slice(end)
  const caret = start + text.length
  sawNativeInput = false
  writing = true
  let inserted = false
  try {
    document.execCommand('insertText', false, text)
    inserted = currentComposer() === composer
      && composer.value === result
      && composer.selectionStart === caret
      && composer.selectionEnd === caret
    if (inserted && imeComposing) remember(composer)
  } finally {
    writing = false
  }
  if (!inserted) {
    if (sawNativeInput) cancel()
    else checkCaret()
  }
  return inserted ? 'inserted' : 'rejected'
}

/** Preedit is independent of the native document and its undo history. See ADR 0056. */
export function showPreedit(text: string, selectionStart: number, _selectionEnd: number): void {
  checkCaret()
  const composer = currentComposer()
  if (composer == null) {
    clearPreedit()
    return
  }
  if (!imeComposing) setImeComposing(true)
  displayedComposer = composer
  display.show(composer, text, selectionStart)
}

export function clearPreedit(): void {
  displayedComposer = null
  display.clear()
}

function currentComposer(): HTMLInputElement | null {
  const composer = findComposer()
  return composer?.isConnected === true && hasComposerFocus() ? composer : null
}

function remember(composer: HTMLInputElement): void {
  composerBasis = composer
  expectedValue = composer.value
  expectedStart = composer.selectionStart ?? 0
  expectedEnd = composer.selectionEnd ?? expectedStart
}

function unwatch(): void {
  composerBasis = null
  document.removeEventListener('selectionchange', checkCaret, true)
  document.removeEventListener('input', checkCaret, true)
  document.removeEventListener('pointerup', checkCaretLater, true)
}

function checkCaretLater(): void {
  queueMicrotask(checkCaret)
}

function checkCaret(event?: Event): void {
  if (writing && event?.type === 'input') sawNativeInput = true
  if (!imeComposing || writing) return
  const composer = currentComposer()
  if (composer !== composerBasis || composer == null
    || composer.value !== expectedValue
    || composer.selectionStart !== expectedStart
    || composer.selectionEnd !== expectedEnd) {
    cancel()
  }
}

function cancel(): void {
  clearPreedit()
  if (!imeComposing) return
  imeComposing = false
  unwatch()
  cancelComposition?.()
}
