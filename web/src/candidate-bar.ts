import barStyles from './candidate-bar.css'
import type { CompositionView, SwitchIconView } from './ime-view'
import { requestSwitch } from './native-bridge'
import { ensurePageStyle } from './page-style'

const BarId = 'keyboard-ime-bar'
const StyleId = 'keyboard-ime-bar-style'
const ContainerId = 'keyboard-container'
const BackgroundId = 'keyboard-background'

/** The stylesheet names the bar by a placeholder, so the id lives in one place. */
const BarStyle = barStyles.replaceAll('#bar-id', `#${BarId}`)

export interface CandidateBar {
  render: (view: CompositionView | null) => void
  showSwitchIcon: (view: SwitchIconView | null) => void
}

/**
 * The candidate bar: one row between the toolbar and the keys, owned by this script.
 * It shows the candidates, the IME name and the IME's own icon, which the switch draws
 * and presses through the plugin. See ADR 0004, ADR 0018 and ADR 0028.
 * The page rebuilds the whole keyboard tree on every layout update, so the bar is put
 * back whenever it is not a child of the current container. See ADR 0004.
 */
export function createCandidateBar(): CandidateBar {
  const bar = document.createElement('div')
  bar.id = BarId

  const candidates = document.createElement('div')
  candidates.className = 'ime-candidates'
  const switchButton = createSwitchButton()
  const chip = document.createElement('div')
  chip.className = 'ime-chip'
  bar.append(candidates, switchButton, chip)

  function render(view: CompositionView | null): void {
    const shown = view?.candidates ?? []
    candidates.replaceChildren(
      ...shown.map((text, index) => createCandidate(text, index, view)),
    )
    chip.hidden = view == null || view.ime.length === 0
    chip.textContent = view?.ime ?? ''
  }

  function showSwitchIcon(view: SwitchIconView | null): void {
    const canvas = switchButton.querySelector<HTMLCanvasElement>('canvas')
    const fallback = switchButton.querySelector<HTMLElement>('.ime-switch-fallback')
    if (canvas == null || fallback == null) {
      return
    }

    if (view == null || view.width <= 0 || view.height <= 0) {
      canvas.hidden = true
      fallback.hidden = false
      return
    }

    const context = canvas.getContext('2d')
    if (context == null) {
      return
    }

    canvas.width = view.width
    canvas.height = view.height
    context.putImageData(
      new ImageData(decodeRgba(view.rgba), view.width, view.height),
      0,
      0,
    )
    canvas.hidden = false
    fallback.hidden = true
  }

  function createCandidate(
    text: string,
    index: number,
    view: CompositionView | null,
  ): HTMLElement {
    const element = document.createElement('div')
    element.className = 'ime-candidate'
    element.classList.toggle('ime-selected', view != null && index === view.selected)

    const number = document.createElement('span')
    number.className = 'ime-number'
    number.textContent = String(index + 1)

    const label = document.createElement('span')
    label.className = 'ime-text'
    label.textContent = text

    element.append(number, label)
    return element
  }

  ensurePageStyle(StyleId, BarStyle)
  render(null)
  showSwitchIcon(null)
  mount()
  observeRebuilds()
  return { render, showSwitchIcon }

  function mount(): void {
    const container = document.getElementById(ContainerId)
    if (container == null || bar.parentElement === container) {
      return
    }

    const background = document.getElementById(BackgroundId)
    container.insertBefore(
      bar,
      background?.parentElement === container ? background : null,
    )
  }

  function observeRebuilds(): void {
    const observer = new MutationObserver(mount)
    observer.observe(document.documentElement, { childList: true, subtree: true })
  }
}

/**
 * The switch: it draws the icon the Windows input indicator draws, and asks the plugin
 * for a press when tapped. A tap must not move the composer's focus, or the keys would
 * stop reaching the IME, so the press is taken on pointer down.
 */
function createSwitchButton(): HTMLElement {
  const button = document.createElement('div')
  button.className = 'ime-switch'
  button.tabIndex = -1

  const canvas = document.createElement('canvas')
  canvas.hidden = true
  const fallback = document.createElement('span')
  fallback.className = 'ime-switch-fallback'
  fallback.textContent = '中/英'
  button.append(canvas, fallback)

  button.addEventListener('pointerdown', event => {
    event.preventDefault()
    requestSwitch()
  })

  return button
}

/** The plugin sends straight RGBA; the canvas wants bytes it can clamp. */
function decodeRgba(encoded: string): Uint8ClampedArray<ArrayBuffer> {
  const binary = atob(encoded)
  const bytes = new Uint8ClampedArray(new ArrayBuffer(binary.length))
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes
}
