/**
 * The keyboard toolbar's text composer, which is the only writeback target and the
 * only input the composition takes over. See ADR 0007, ADR 0008 and ADR 0019.
 */
const ComposerId = 'keyboard-voice-input'
const SendButtonId = 'keyboard-voice-send'
const UnderlineId = 'keyboard-ime-preedit-underline'
const UnderlineColor = '#5cfbe9'

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

/** The composer the user last put the caret into. */
let focused: HTMLInputElement | null = null

/**
 * The page rebuilds its toolbar on layout and state updates, which replaces the input
 * and takes the focus with it. Put the focus back only when the old input was removed;
 * a click on another control leaves the old input in place, so the user wins.
 */
export function keepComposerFocus(): void {
  document.addEventListener(
    'focusin',
    (event) => {
      const composer = findComposer()
      if (composer != null && event.target === composer) {
        focused = composer
      }
    },
    true,
  )

  const observer = new MutationObserver(() => {
    if (focused == null || focused.isConnected) {
      return
    }

    const composer = findComposer()
    if (composer == null) {
      focused = null
      return
    }

    focused = composer
    composer.focus()
  })
  observer.observe(document.documentElement, { childList: true, subtree: true })
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

  return document.execCommand('insertText', false, text) ? 'inserted' : 'rejected'
}

/**
 * Shows the codes the IME is still composing, after the text already in the composer,
 * the way a native IME does. The value and caret are put back once the IME commits.
 * See ADR 0019.
 */
export function showPreedit(text: string): void {
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

  const before = baseValue.slice(0, baseStart)
  const after = baseValue.slice(baseEnd)
  composer.value = before + text + after
  composer.setSelectionRange(baseStart + text.length, baseStart + text.length)
  holdSendButton()
  drawUnderline(composer, before, text)
}

/** Puts the composer back the way the page left it, once the IME has committed. */
export function clearPreedit(): void {
  if (!composing) {
    return
  }

  composing = false
  const composer = findComposer()
  if (composer == null) {
    return
  }

  composer.value = baseValue
  composer.setSelectionRange(baseStart, baseEnd)
  releaseSendButton(composer)
  hideUnderline()
  observer?.disconnect()
  observer = null
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
