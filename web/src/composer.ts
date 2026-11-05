/**
 * The keyboard toolbar's text composer, which is the only writeback target and the
 * only input the composition takes over. See ADR 0007 and ADR 0008.
 */
import { ensurePageStyle } from './page-style'

const ComposerId = 'keyboard-voice-input'
const PreeditClass = 'ime-composing'
const StyleId = 'keyboard-ime-composer-style'

const PreeditStyle = `
#${ComposerId}.${PreeditClass}::placeholder {
  color: #9fd8e6; letter-spacing: .06em; opacity: 1;
}
`

export type WritebackOutcome = 'inserted' | 'empty' | 'focus-lost' | 'rejected'

/** The composing string, or null when the composer shows its own placeholder. */
let preedit: string | null = null
let placeholderBeforePreedit: string | null = null
let observer: MutationObserver | null = null

export function findComposer(): HTMLInputElement | null {
  const element = document.getElementById(ComposerId)
  return element instanceof HTMLInputElement ? element : null
}

/**
 * Writes committed text with execCommand so Chromium keeps its undo stack, caret and
 * input events. The focus check guards against writing into another field. See ADR 0008.
 */
export function insertText(text: string): WritebackOutcome {
  if (text === '') {
    return 'empty'
  }

  const composer = findComposer()
  if (composer == null || document.activeElement !== composer) {
    return 'focus-lost'
  }

  return document.execCommand('insertText', false, text) ? 'inserted' : 'rejected'
}

/**
 * Shows the codes the IME is still composing. The composer's value stays untouched, so
 * the send button stays disabled until the IME commits. The page rewrites the
 * placeholder for its own status texts, hence the reassertion below.
 */
export function showPreedit(text: string): void {
  if (preedit === text) {
    return
  }

  preedit = text
  watch()
  applyPreedit()
}

/** Gives the placeholder back to the page once the IME has committed. */
export function clearPreedit(): void {
  if (preedit === null) {
    return
  }

  preedit = null
  observer?.disconnect()
  observer = null

  const composer = findComposer()
  if (composer != null) {
    composer.classList.remove(PreeditClass)
    if (placeholderBeforePreedit != null) {
      composer.placeholder = placeholderBeforePreedit
    }
  }

  placeholderBeforePreedit = null
}

function applyPreedit(): void {
  if (preedit === null) {
    return
  }

  const composer = findComposer()
  if (composer == null) {
    return
  }

  // The page owns the placeholder text; keep the first one it wrote and put it back.
  placeholderBeforePreedit ??= composer.placeholder

  ensurePageStyle(StyleId, PreeditStyle)
  composer.classList.add(PreeditClass)
  if (composer.placeholder !== preedit) {
    composer.placeholder = preedit
  }
}

function watch(): void {
  if (observer != null) {
    return
  }

  observer = new MutationObserver(applyPreedit)
  observer.observe(document.documentElement, {
    childList: true,
    subtree: true,
    attributes: true,
    attributeFilter: ['placeholder'],
  })
}
