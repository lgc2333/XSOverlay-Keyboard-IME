/* eslint no-console: ["error", { allow: ["debug", "warn", "error"] }] -- Correlate Return requests without logging composer text. */
import { findComposer } from './composer'
import { requestReturn } from './native-bridge'
import { createReturnFeedback } from './return-feedback'
import { loadNativeKeyboardInput } from './native-keyboard-input'
import type { NativeKeyboardInput, NativeRepeatController } from './native-keyboard-input'

/** The host decides each Return before the page can send uncommitted text. */
export function routeComposerReturn(): {
  cancel: () => void
  resolve: (id: number, taken: boolean) => void
  sound: ReturnType<typeof createReturnFeedback>['sound']
} {
  const feedback = createReturnFeedback()
  let sequence = 0
  const pending = new Map<number, { idle: () => void; taken: () => void }>()
  const replayed = new WeakSet<Event>()
  const presses = new Map<string, { end: () => void }>()
  let input: Promise<NativeKeyboardInput> | undefined
  let cancellationVersion = 0

  function ask(
    event: Event,
    replay: () => void,
    taken: () => void = () => {},
  ): number | undefined {
    const id = ++sequence
    pending.set(id, { idle: replay, taken })
    console.debug('[xso-ime] Return request', id, event.type)
    if (!requestReturn(id)) {
      pending.delete(id)
      console.debug('[xso-ime] Return bridge unavailable', id)
      return undefined
    }
    event.preventDefault()
    event.stopImmediatePropagation()
    console.debug('[xso-ime] Return bridge sent', id)
    return id
  }

  function replay(target: EventTarget, event: Event): void {
    if (document.activeElement !== findComposer()) return
    replayed.add(event)
    target.dispatchEvent(event)
  }

  document.addEventListener(
    'keydown',
    (event) => {
      if (
        event.key !== 'Enter' ||
        event.target !== findComposer() ||
        replayed.has(event)
      ) {
        return
      }
      const target = event.target
      if (target == null) return
      ask(event, () => replay(target, new KeyboardEvent('keydown', event)))
    },
    true,
  )

  function begin(
    event: Event,
    id: string,
    key: HTMLElement,
    clone: () => Event,
    release: () => Event,
    hapticTarget: string,
  ): void {
    if (presses.has(id) || window.vuplex == null) return
    event.preventDefault()
    event.stopImmediatePropagation()
    let repeat: NativeRepeatController | undefined
    let nativeInput: NativeKeyboardInput | undefined
    let firstTaken = false
    let held = true
    const version = cancellationVersion
    const animationKey = key.closest<HTMLElement>('.key-shape-wrapper') ?? key
    function current(): boolean {
      return version === cancellationVersion
        && key.isConnected
        && document.activeElement === findComposer()
    }
    function end(): void {
      held = false
      repeat?.dispose()
      if (presses.get(id)?.end === end) presses.delete(id)
    }
    presses.set(id, { end })
    // Loading the repeat controller must not swallow the first intercepted Return.
    const firstRequest = ask(
      event,
      () => {
        const wasHeld = held
        end()
        replay(key, clone())
        if (!wasHeld) key.dispatchEvent(release())
      },
      () => {
        firstTaken = true
        nativeInput?.animate(animationKey)
        feedback.press(key, hapticTarget)
      },
    )
    if (firstRequest == null) {
      end()
      return
    }
    input ??= loadNativeKeyboardInput()
    void input
      .then((native) => {
        console.debug('[xso-ime] Return controller ready', firstRequest)
        if (!current()) {
          end()
          return
        }
        nativeInput = native
        if (firstTaken) native.animate(animationKey)
        if (!held) return
        repeat = native.createRepeatController({
          send: (repeated) => {
            // Native begin() sends immediately; the first request is already in flight.
            if (!repeated) return
            if (!current()) {
              end()
              return
            }
            native.animate(animationKey)
            ask(event, () => {})
          },
        })
        repeat.begin(id, hapticTarget)
        if (!held) repeat.dispose()
      })
      .catch((error) => {
        end()
        console.warn('[xso-ime] Return repeat unavailable', firstRequest, error)
      })
  }

  document.addEventListener(
    'pointerdown',
    (event) => {
      if (
        event.pointerType === 'touch' ||
        replayed.has(event) ||
        document.activeElement !== findComposer()
      ) {
        return
      }
      const key =
        event.target instanceof Element
          ? event.target.closest<HTMLElement>('[id="key-RETURN"]')
          : null
      if (key != null) {
        begin(
          event,
          `pointer:${event.pointerId}`,
          key,
          () => new PointerEvent('pointerdown', event),
          () =>
            new PointerEvent('pointerup', {
              bubbles: true,
              pointerId: event.pointerId,
              pointerType: event.pointerType,
            }),
          'keyboard',
        )
      }
    },
    true,
  )
  document.addEventListener(
    'touchstart',
    (event) => {
      if (replayed.has(event) || document.activeElement !== findComposer()) return
      const key =
        event.target instanceof Element
          ? event.target.closest<HTMLElement>('[id="key-RETURN"]')
          : null
      if (key == null) return
      for (const touch of event.changedTouches) {
        begin(
          event,
          `touch:${touch.identifier}`,
          key,
          () =>
            new TouchEvent('touchstart', {
              bubbles: true,
              cancelable: true,
              changedTouches: [touch],
              touches: [touch],
              targetTouches: [touch],
            }),
          () =>
            new TouchEvent('touchend', {
              bubbles: true,
              cancelable: true,
              changedTouches: [touch],
            }),
          touch.identifier === 1
            ? 'leftcontroller'
            : touch.identifier === 2
              ? 'rightcontroller'
              : 'keyboard',
        )
      }
    },
    { capture: true, passive: false },
  )

  for (const type of ['pointerup', 'pointercancel', 'lostpointercapture']) {
    document.addEventListener(
      type,
      (event) => {
        presses.get(`pointer:${(event as PointerEvent).pointerId}`)?.end()
      },
      true,
    )
  }
  for (const type of ['touchend', 'touchcancel']) {
    document.addEventListener(
      type,
      (event) => {
        for (const touch of (event as TouchEvent).changedTouches)
          presses.get(`touch:${touch.identifier}`)?.end()
      },
      true,
    )
  }
  function clear(reason: string): void {
    console.debug('[xso-ime] Return cancelled', reason, [...pending.keys()])
    cancellationVersion++
    for (const press of presses.values()) press.end()
    pending.clear()
  }
  window.addEventListener('blur', () => clear('window-blur'))
  window.addEventListener('pagehide', () => {
    clear('pagehide')
    void input?.then((native) => native.dispose(), () => {})
  })
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) clear('document-hidden')
  })

  return {
    cancel: () => clear('focus-revoked'),
    sound: feedback.sound,
    resolve: (id, taken) => {
      const continuation = pending.get(id)
      pending.delete(id)
      console.debug('[xso-ime] Return decision', id, continuation == null ? 'cancelled-or-resolved' : taken ? 'taken' : 'idle')
      if (taken) continuation?.taken()
      else continuation?.idle()
    },
  }
}
