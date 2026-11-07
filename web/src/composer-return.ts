import { findComposer } from './composer'
import { requestReturn } from './native-bridge'
import { createReturnFeedback } from './return-feedback'
import { loadNativeKeyboardInput } from './native-keyboard-input'
import type { NativeKeyboardInput, NativeRepeatController } from './native-keyboard-input'

/** The host decides each Return before the page can send uncommitted text. */
export function routeComposerReturn(): {
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
  ): boolean {
    const id = ++sequence
    pending.set(id, { idle: replay, taken })
    if (!requestReturn(id)) {
      pending.delete(id)
      return false
    }
    event.preventDefault()
    event.stopImmediatePropagation()
    return true
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
    let held = true
    const version = cancellationVersion
    function end(): void {
      held = false
      repeat?.dispose()
      if (presses.get(id)?.end === end) presses.delete(id)
    }
    presses.set(id, { end })
    input ??= loadNativeKeyboardInput()
    void input
      .then((native) => {
        if (
          version !== cancellationVersion ||
          !key.isConnected ||
          document.activeElement !== findComposer()
        ) {
          end()
          return
        }
        const animationKey = key.closest<HTMLElement>('.key-shape-wrapper') ?? key
        repeat = native.createRepeatController({
          send: (repeated) => {
            if (!key.isConnected || document.activeElement !== findComposer()) {
              end()
              return
            }
            if (repeated) native.animate(animationKey)
            ask(
              event,
              () => {
                if (repeated) return
                const wasHeld = held
                end()
                replay(key, clone())
                if (!wasHeld) key.dispatchEvent(release())
              },
              () => {
                if (!repeated) {
                  native.animate(animationKey)
                  feedback.press(key, hapticTarget)
                }
              },
            )
          },
        })
        repeat.begin(id, hapticTarget)
        if (!held) repeat.dispose()
      })
      .catch((error) => {
        end()
        console.warn('[xso-ime] Return input failed', error)
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
  function clear(): void {
    cancellationVersion++
    for (const press of presses.values()) press.end()
    pending.clear()
  }
  window.addEventListener('blur', clear)
  window.addEventListener('pagehide', () => {
    clear()
    void input?.then((native) => native.dispose())
  })
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) clear()
  })

  return {
    sound: feedback.sound,
    resolve: (id, taken) => {
      const continuation = pending.get(id)
      pending.delete(id)
      if (taken) continuation?.taken()
      else continuation?.idle()
    },
  }
}
