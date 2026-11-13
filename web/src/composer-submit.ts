import { hasComposerFocus } from './composer-focus'
import { requestSubmit } from './native-bridge'

const SendButtonId = 'keyboard-voice-send'
const SubmitError = 'Send failed. Try again.'

/** Defers the page's own send action until the host has restored its old target. */
export function installComposerSubmit() {
  let pending: { button: HTMLElement | null; replay: () => void } | null = null
  let replaying = false
  let restoreError: (() => void) | null = null

  function buttonFrom(event: Event): HTMLElement | null {
    const target = event.target
    return target instanceof Element
      ? target.closest<HTMLElement>(`#${SendButtonId}`)
      : null
  }

  function intercept(event: Event): void {
    if (replaying) return
    const button = buttonFrom(event)
    if (button == null) return
    if (
      event.type === 'pointerdown' &&
      event instanceof PointerEvent &&
      event.button !== 0
    ) {
      return
    }
    if (pending != null) {
      event.preventDefault()
      event.stopImmediatePropagation()
      return
    }
    if (!hasComposerFocus()) return
    const replay =
      event.type === 'pointerdown' && event instanceof PointerEvent
        ? () => {
            button.dispatchEvent(
              new PointerEvent('pointerdown', {
                bubbles: true,
                cancelable: true,
                button: event.button,
                buttons: event.buttons,
                clientX: event.clientX,
                clientY: event.clientY,
                pointerId: event.pointerId,
                pointerType: event.pointerType,
              }),
            )
          }
        : () => button.click()
    if (!defer(replay, button)) {
      return
    }
    event.preventDefault()
    event.stopImmediatePropagation()
  }

  function defer(replay: () => void, button: HTMLElement | null = null): boolean {
    if (pending != null || !hasComposerFocus()) return false
    pending = { button, replay }
    if (requestSubmit()) return true
    pending = null
    return false
  }

  function showError(): void {
    const error = document.getElementById('keyboard-voice-inline-error')
    if (!(error instanceof HTMLElement)) return
    const wrap = document.getElementById('keyboard-composer-wrap')
    if (restoreError == null) {
      const text = error.textContent ?? ''
      const title = error.title
      const hidden = error.hidden
      const hadError = wrap?.classList.contains('voice-error') ?? false
      restoreError = () => {
        if (error.textContent !== SubmitError) return
        error.textContent = text
        error.title = title
        error.hidden = hidden
        wrap?.classList.toggle('voice-error', hadError)
      }
    }
    error.textContent = SubmitError
    error.title = SubmitError
    error.hidden = false
    wrap?.classList.add('voice-error')
  }

  function clearError(): void {
    const restore = restoreError
    restoreError = null
    restore?.()
  }

  document.addEventListener('pointerdown', intercept, true)
  document.addEventListener('click', intercept, true)
  document.addEventListener('input', clearError, true)

  return {
    defer,
    clear(restored: boolean): void {
      const queued = pending
      pending = null
      if (queued == null) return
      if (!restored || (queued.button != null && !queued.button.isConnected)) {
        showError()
        return
      }
      clearError()
      replaying = true
      try {
        queued.replay()
      } finally {
        replaying = false
      }
    },
  }
}
