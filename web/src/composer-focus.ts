import { reportComposerActive } from './native-bridge'

const ComposerId = 'keyboard-voice-input'

let focused: HTMLInputElement | null = null
let viewActive = !document.hidden
let reportedActive = false
let localActive = false

export function findComposer(): HTMLInputElement | null {
  const element = document.getElementById(ComposerId)
  return element instanceof HTMLInputElement ? element : null
}

/** DOM focus alone survives a whole-view blur. See ADR 0037. */
export function hasComposerFocus(): boolean {
  const composer = findComposer()
  return viewActive && composer != null && document.activeElement === composer
}

/** Restores only the composer that still owned focus when its node was replaced. */
export function observeComposerFocus(
  onActiveChange: (active: boolean) => void,
  onRebuilt: (composer: HTMLInputElement) => void,
): void {
  focused = hasComposerFocus() ? findComposer() : null

  function applyActive(active: boolean): void {
    if (active !== localActive) {
      localActive = active
      onActiveChange(active)
    }
  }

  function reportActive(): void {
    const active = hasComposerFocus()
    focused = active ? findComposer() : null
    applyActive(active)
    if (active !== reportedActive) {
      reportedActive = active
      reportComposerActive(active)
    }
  }

  function reportActiveLater(): void {
    setTimeout(reportActive, 0)
  }

  function setViewActive(active: boolean): void {
    viewActive = active
    if (!active) {
      focused = null
      applyActive(false)
    }
    reportActiveLater()
  }

  document.addEventListener('focusin', (event) => {
    const composer = findComposer()
    if (composer != null && event.target === composer && viewActive) {
      focused = composer
      applyActive(true)
    } else {
      focused = null
    }
    reportActiveLater()
  }, true)
  document.addEventListener('focusout', reportActiveLater, true)
  window.addEventListener('blur', () => setViewActive(false))
  window.addEventListener('focus', () => setViewActive(true))
  document.addEventListener('visibilitychange', () => setViewActive(!document.hidden))

  const observer = new MutationObserver(() => {
    if (!viewActive || focused == null || focused.isConnected) {
      return
    }

    const active = document.activeElement
    const composer = findComposer()
    // Another control already took focus; replacing the old node grants no claim.
    if (active != null && active !== document.body && active !== composer) {
      focused = null
      return
    }
    if (composer != null) {
      focused = composer
      composer.focus()
      onRebuilt(composer)
    }
  })
  observer.observe(document.documentElement, { childList: true, subtree: true })
  reportActive()
}
