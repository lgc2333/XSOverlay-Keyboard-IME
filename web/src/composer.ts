import { reportComposerActive } from './native-bridge'

/**
 * The keyboard toolbar's text composer, which is the only writeback target and the
 * only input the composition takes over. See ADR 0007, ADR 0008 and ADR 0019.
 */
const ComposerId = 'keyboard-voice-input'
const SendButtonId = 'keyboard-voice-send'
const UnderlineId = 'keyboard-ime-preedit-underline'
const UnderlineColor = '#5cfbe9'

/** How long a rebuild has to put the focus back before a blur counts as the user leaving. */
const BlurSettleMilliseconds = 60

export type WritebackOutcome = 'inserted' | 'empty' | 'focus-lost' | 'rejected'

/** The value and caret the composer held before the composing string was added. */
let baseValue = ''
let baseStart = 0
let baseEnd = 0
let composing = false
let observer: MutationObserver | null = null
let measure: CanvasRenderingContext2D | null = null

/**
 * An input cannot colour part of its value, so the composing codes get an underline
 * drawn on top of them, the way a native IME marks its preedit.
 */
let underline: HTMLDivElement | null = null

/** The composer the user last put the caret into; a rebuild replaces it. */
let focused: HTMLInputElement | null = null

/** Whether the plugin was last told the composer holds the focus. See ADR 0035. */
let reportedActive = false

/** What the page last told its own layout about the focus. See ADR 0037. */
let localActive = false

/** Whether the page itself is the active view, which a focusout alone does not tell. */
let viewActive = !document.hidden

/** Told as soon as the composer gains or loses the focus, before the plugin answers. */
let onFocusChange: ((active: boolean) => void) | null = null

/** The value the IME owns while it composes, so anyone else's edit is noticed. See ADR 0037. */
let expectedValue = ''

/** The codes the IME last reported, so a rebuilt composer can show them again. */
let lastPreedit: { text: string; start: number; end: number } | null = null

/** The input the codes were written into; a different one means the page rebuilt it. */
let composingComposer: HTMLInputElement | null = null

/** Counts compositions, so a timer armed by a finished one cannot touch the next. */
let compositionEpoch = 0

/** True while the plugin reports a composition, which needs no visible codes. */
let imeComposing = false

/** The selection the page put into the composer, so a move by anyone else is noticed. */
let expectedSelectionStart = 0
let expectedSelectionEnd = 0

/** Told when the composition no longer fits the composer. */
let cancelComposition: (() => void) | null = null

/**
 * The page rebuilds its toolbar on layout and state updates, which replaces the input
 * and takes the focus with it. Put the focus back only when the old input was removed;
 * a click on another control leaves the old input in place, so the user wins.
 */
export function keepComposerFocus(onActiveChange: (active: boolean) => void): void {
  onFocusChange = onActiveChange
  const composer = findComposer()
  focused = document.activeElement === composer ? composer : null

  document.addEventListener(
    'focusin',
    (event) => {
      const composer = findComposer()
      if (composer != null && event.target === composer) {
        focused = composer

        // The layout follows the focus at once, not the plugin's answer. See ADR 0037.
        if (viewActive) {
          applyActive(true)
        }
      }

      reportActiveLater()
    },
    true,
  )

  document.addEventListener('focusout', reportActiveLater, true)

  // Losing the whole page counts as losing the composer, even with no focusout. See ADR 0037.
  window.addEventListener('blur', () => setViewActive(false))
  window.addEventListener('focus', () => setViewActive(true))
  document.addEventListener('visibilitychange', () => setViewActive(!document.hidden))

  // A rebuild removes the input the focus sat on, and the focus stays on that removed
  // node, so the new input never gets it. Whatever was lost, the composer takes it back.
  const observer = new MutationObserver(reclaimFocus)
  observer.observe(document.documentElement, { childList: true, subtree: true })
  reclaimFocus()
  reportActive()
}

/**
 * Puts the focus back on the composer when the page replaced it under the user. A blur
 * the user chose leaves the composer connected, so it is not reclaimed.
 */
function reclaimFocus(): void {
  const active = document.activeElement
  const replaced =
    (focused != null && !focused.isConnected) ||
    (composingComposer != null && !composingComposer.isConnected)
  if (!replaced && active != null) {
    return
  }

  const composer = findComposer()
  if (composer == null || composer === active) {
    return
  }

  focused = composer
  composer.focus()

  // The page replaced the input the codes were in. A rebuilt composer is not a blur:
  // the codes move across, or the new input already carries them. See ADR 0034.
  adoptRebuiltComposer(composer)
}

/**
 * Decides what the codes do when the page swapped the input under them. The page either
 * copies the value across or starts the new input empty; anything else means the
 * composition no longer fits what is there.
 */
function adoptRebuiltComposer(composer: HTMLInputElement): void {
  if (!composing || lastPreedit == null) {
    return
  }

  const codes = lastPreedit
  const composed =
    baseValue.slice(0, baseStart) + codes.text + baseValue.slice(baseEnd)
  if (composer.value === composed) {
    composingComposer = composer
    expectedValue = composed
    const start = baseStart + Math.max(0, Math.min(codes.text.length, codes.start))
    const end = baseStart + Math.max(0, Math.min(codes.text.length, codes.end))
    expectedSelectionStart = start
    expectedSelectionEnd = end
    composer.setSelectionRange(start, end)
    return
  }

  if (composer.value === baseValue) {
    showPreedit(codes.text, codes.start, codes.end)
    return
  }

  cancel()
}

/**
 * Tells the plugin whether the composer holds the focus, once per change. A focus move
 * settles after the event, and a rebuild refocuses the new input, so the check waits for
 * the next task instead of trusting the event. See ADR 0035.
 */
function reportActiveLater(): void {
  setTimeout(reportActive, 0)
}

function setViewActive(value: boolean): void {
  viewActive = value
  reportActiveLater()
}

function reportActive(): void {
  const active = viewActive && document.activeElement === findComposer()
  applyActive(active)
  if (active === reportedActive) {
    return
  }

  reportedActive = active
  reportComposerActive(active)
}

/** Tells the page what the focus did, once per change, as soon as the change is known. */
function applyActive(active: boolean): void {
  if (active === localActive) {
    return
  }

  localActive = active
  onFocusChange?.(active)
}

/** Arms the composition watch while the plugin reports a composition. See ADR 0027. */
export function setImeComposing(value: boolean): void {
  if (value === imeComposing) {
    return
  }

  imeComposing = value
  compositionEpoch += 1
  if (imeComposing) {
    const composer = findComposer()
    if (composer != null) {
      expectedValue = composer.value
      expectedSelectionStart = composer.selectionStart ?? 0
      expectedSelectionEnd = composer.selectionEnd ?? expectedSelectionStart
    }

    watchCaret()
  } else {
    unwatchCaret()
  }
}

/**
 * Reports a composition that no longer fits the composer: the user moved the caret,
 * clicked elsewhere, or the composer went away. See ADR 0027.
 */
export function onCompositionCancel(callback: () => void): void {
  cancelComposition = callback
}

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

  // The IME's text replaces the codes it was composing, so the codes go first. If the
  // browser refuses the write, the codes go back rather than vanishing.
  const codes = imeComposing ? lastPreedit : null
  if (imeComposing) {
    imeComposing = false
    unwatchCaret()
    clearPreedit()
  }

  if (document.execCommand('insertText', false, text)) {
    return 'inserted'
  }

  if (codes != null) {
    imeComposing = true
    showPreedit(codes.text, codes.start, codes.end)
    watchCaret()
  }

  return 'rejected'
}

/**
 * Shows the codes the IME is still composing, after the text already in the composer,
 * the way a native IME does. The value and caret are put back once the IME commits.
 * See ADR 0019.
 */
export function showPreedit(text: string, selectionStart: number, selectionEnd: number): void {
  const composer = findComposer()
  if (composer == null) {
    return
  }

  if (!composing) {
    baseValue = composer.value
    baseStart = composer.selectionStart ?? baseValue.length
    baseEnd = composer.selectionEnd ?? baseStart
    composing = true
    watchRebuilds()
  }

  // Every report says which input now holds the codes: a rebuild replaces it. See ADR 0034.
  composingComposer = composer

  const before = baseValue.slice(0, baseStart)
  const after = baseValue.slice(baseEnd)
  composer.value = before + text + after
  expectedValue = composer.value
  lastPreedit = { text, start: selectionStart, end: selectionEnd }

  // The IME owns the caret inside its codes; draw it where the IME says it is.
  const start = Math.max(0, Math.min(text.length, selectionStart))
  const end = Math.max(start, Math.min(text.length, selectionEnd))
  expectedSelectionStart = baseStart + start
  expectedSelectionEnd = baseStart + end
  composer.setSelectionRange(expectedSelectionStart, expectedSelectionEnd)
  holdSendButton()
  drawUnderline(composer, before, text)
}

/** Puts the composer back the way the page left it, once the IME has committed. */
export function clearPreedit(): void {
  if (!composing) {
    return
  }

  composing = false
  lastPreedit = null
  composingComposer = null
  hideUnderline()
  observer?.disconnect()
  observer = null
  const composer = findComposer()
  if (composer == null) {
    return
  }

  composer.value = baseValue
  composer.setSelectionRange(baseStart, baseEnd)
  // The page just moved the value and the caret, so the next check must not read that as
  // somebody else's edit. See ADR 0037.
  expectedValue = composer.value
  expectedSelectionStart = baseStart
  expectedSelectionEnd = baseEnd
  releaseSendButton(composer)
}

/** The page enables send on any non-empty value, so keep it off while composing. */
function holdSendButton(): void {
  const send = document.getElementById(SendButtonId)
  if (send instanceof HTMLButtonElement) {
    send.disabled = true
  }
}

/** A rebuild replaces the composer and the send button, so the holds follow them. */
function watchRebuilds(): void {
  observer ??= new MutationObserver(reassert)
  observer.observe(document.documentElement, { childList: true, subtree: true })
}

function reassert(): void {
  if (!composing) {
    return
  }

  holdSendButton()
}

/**
 * Watches for a caret the page did not put there. The composer reports its own
 * selection changes, and a ray click moves the caret without a key.
 */
function watchCaret(): void {
  document.addEventListener('selectionchange', checkCaret, true)
  document.addEventListener('pointerup', checkCaretLater, true)
  document.addEventListener('focusout', checkFocusLater, true)
}

function unwatchCaret(): void {
  document.removeEventListener('selectionchange', checkCaret, true)
  document.removeEventListener('pointerup', checkCaretLater, true)
  document.removeEventListener('focusout', checkFocusLater, true)
}

/**
 * A rebuild clears the toolbar's state before it puts the new input in, and the blur it
 * causes is page mechanics, not the user leaving. The focus has time to come back.
 * See ADR 0034.
 */
function checkFocusLater(): void {
  afterComposition(BlurSettleMilliseconds, checkFocus)
}

/**
 * Runs a check unless the composition it belongs to has already ended, so a timer left
 * by the last one cannot cancel the next. See ADR 0027 and ADR 0037.
 */
function afterComposition(delayMilliseconds: number, check: () => void): void {
  const epoch = compositionEpoch
  setTimeout(() => {
    if (epoch === compositionEpoch) {
      check()
    }
  }, delayMilliseconds)
}

function checkFocus(): void {
  if (!imeComposing) {
    return
  }

  const composer = findComposer()
  if (composer == null) {
    cancelIfGone()
    return
  }

  // The codes were written into another input, so the page rebuilt its toolbar: that is
  // not the user leaving. See ADR 0034.
  if (composingComposer != null && composer !== composingComposer) {
    return
  }

  if (document.activeElement !== composer) {
    cancel()
  }
}

/** Cancels only if the composer is still gone on the next task, not mid-rebuild. */
function cancelIfGone(): void {
  afterComposition(0, () => {
    if (imeComposing && findComposer() == null) {
      cancel()
    }
  })
}

/** A click settles the caret after the event, so the check waits for the next task. */
function checkCaretLater(): void {
  afterComposition(0, checkCaret)
}

function checkCaret(): void {
  if (!imeComposing) {
    return
  }

  const composer = findComposer()
  if (composer == null) {
    cancelIfGone()
    return
  }

  // The IME owns the value and the caret; an edit by anyone else cancels. See ADR 0037.
  if (
    composer.value !== expectedValue ||
    composer.selectionStart !== expectedSelectionStart ||
    composer.selectionEnd !== expectedSelectionEnd
  ) {
    cancel()
  }
}

/**
 * Gives the composer back to the page and asks the plugin to cancel the IME's work.
 * A value somebody else wrote is kept: restoring it would undo the edit that caused
 * the cancel, such as the clear button. See ADR 0037.
 */
function cancel(): void {
  if (!imeComposing) {
    return
  }

  imeComposing = false
  compositionEpoch += 1
  unwatchCaret()
  const composer = findComposer()
  if (composer != null && composer.value === expectedValue) {
    clearPreedit()
  } else {
    forgetPreedit()
  }
  cancelComposition?.()
}

/** Drops the page's composition state without touching what the page now holds. */
function forgetPreedit(): void {
  composing = false
  lastPreedit = null
  composingComposer = null
  hideUnderline()
  observer?.disconnect()
  observer = null
  const composer = findComposer()
  if (composer != null) {
    releaseSendButton(composer)
  }
}

function drawUnderline(
  composer: HTMLInputElement,
  before: string,
  codes: string,
): void {
  const context = measureContext()
  if (context == null) {
    return
  }

  const style = getComputedStyle(composer)
  context.font = `${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`
  const box = composer.getBoundingClientRect()
  const paddingLeft = Number.parseFloat(style.paddingLeft) || 0
  const start =
    box.left + paddingLeft + context.measureText(before).width - composer.scrollLeft
  const width = context.measureText(codes).width
  const right = box.right - (Number.parseFloat(style.paddingRight) || 0)

  // Sit the line just below the baseline, not on the input's bottom edge.
  const glyph = context.measureText('Hg')
  const baseline =
    box.top +
    box.height / 2 +
    (glyph.actualBoundingBoxAscent - glyph.actualBoundingBoxDescent) / 2

  if (codes.length === 0 || start >= right || start + width <= box.left + paddingLeft) {
    hideUnderline()
    return
  }

  const element = ensureUnderline()
  element.style.left = `${Math.max(start, box.left + paddingLeft)}px`
  element.style.top = `${baseline + 3}px`
  element.style.width = `${Math.min(width, right - start)}px`
  element.style.display = 'block'
}

function ensureUnderline(): HTMLDivElement {
  if (underline == null || !document.body.contains(underline)) {
    underline = document.createElement('div')
    underline.id = UnderlineId
    Object.assign(underline.style, {
      position: 'fixed',
      height: '2px',
      borderRadius: '999px',
      background: UnderlineColor,
      opacity: '0.65',
      pointerEvents: 'none',
      zIndex: '300',
    })
    document.body.appendChild(underline)
  }

  return underline
}

function hideUnderline(): void {
  if (underline != null) {
    underline.style.display = 'none'
  }
}

function measureContext(): CanvasRenderingContext2D | null {
  measure ??= document.createElement('canvas').getContext('2d')
  return measure
}

/** Puts the page's own rule back: send stays off while the composer is empty. */
function releaseSendButton(composer: HTMLInputElement): void {
  const send = document.getElementById(SendButtonId)
  if (send instanceof HTMLButtonElement) {
    send.disabled = composer.value.trim().length === 0
  }
}
