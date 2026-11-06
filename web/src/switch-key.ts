import type { SwitchIconView } from './ime-view'
import { requestSwitch } from './native-bridge'
import { ensurePageStyle } from './page-style'

const SwitchStyleId = 'keyboard-ime-switch-style'
const SwitchClass = 'ime-switch-key'
const SpaceId = 'key-SPACE'
const ContainerId = 'keyboard-container'

const SwitchStyle = `
#${ContainerId} .${SwitchClass} {
  display: flex; align-items: center; justify-content: center;
  padding: 0; box-sizing: border-box;
  cursor: pointer; -webkit-user-select: none; user-select: none;
}
#${ContainerId} .${SwitchClass} canvas {
  width: 32px; height: 32px; image-rendering: pixelated; pointer-events: none;
}
#${ContainerId} .${SwitchClass} .ime-switch-fallback {
  font-size: 16px; line-height: 1;
}
`

export interface SwitchKey {
  /** Draws the IME's icon on the key, and keeps it after the page rebuilds. */
  show: (view: SwitchIconView | null) => void
}

/**
 * The switch as a key of the keyboard itself, left of the space bar, with the space bar
 * giving up one key of width. The page rebuilds the whole tree on every layout update,
 * so the key is put back whenever it disappears. See ADR 0012 and ADR 0028.
 */
export function createSwitchKey(): SwitchKey {
  let icon: SwitchIconView | null = null

  function show(view: SwitchIconView | null): void {
    icon = view
    const key = document.querySelector<HTMLElement>(`.${SwitchClass}`)
    if (key != null) {
      draw(key, icon)
    }
  }

  ensurePageStyle(SwitchStyleId, SwitchStyle)
  mount()
  observeRebuilds()
  return { show }

  function mount(): void {
    const space = document.getElementById(SpaceId)
    const row = space?.parentElement
    if (
      space == null ||
      row == null ||
      space.previousElementSibling?.classList.contains(SwitchClass)
    ) {
      return
    }

    // One key of width comes off the space bar, so the row keeps its length.
    const unit = Number.parseFloat(space.style.height)
    const width = Number.parseFloat(space.style.width)
    if (!(unit > 0) || !(width > unit)) {
      return
    }

    const key = createKey(space, unit)
    draw(key, icon)
    row.insertBefore(key, space)
    space.style.width = `${width - unit}px`
  }

  function observeRebuilds(): void {
    const observer = new MutationObserver(mount)
    observer.observe(document.documentElement, { childList: true, subtree: true })
  }
}

/** Takes the look of the key beside it: same size, margins and colours. */
function createKey(space: HTMLElement, unit: number): HTMLElement {
  const key = document.createElement('button')
  key.className = `button2 key ${SwitchClass}`
  key.type = 'button'
  Object.assign(key.style, {
    width: `${unit}px`,
    height: space.style.height,
    margin: space.style.margin,
    marginTop: space.style.marginTop,
    position: 'relative',
    pointerEvents: 'auto',
  })

  const canvas = document.createElement('canvas')
  canvas.hidden = true
  const fallback = document.createElement('span')
  fallback.className = 'ime-switch-fallback'
  fallback.textContent = '中/英'
  key.append(canvas, fallback)

  key.addEventListener('pointerdown', (event) => {
    event.preventDefault()
    requestSwitch()
  })

  return key
}

function draw(key: HTMLElement, view: SwitchIconView | null): void {
  const canvas = key.querySelector('canvas')
  const fallback = key.querySelector<HTMLElement>('.ime-switch-fallback')
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

/** The plugin sends straight RGBA; the canvas wants bytes it can clamp. */
function decodeRgba(encoded: string): Uint8ClampedArray<ArrayBuffer> {
  const binary = atob(encoded)
  const bytes = new Uint8ClampedArray(new ArrayBuffer(binary.length))
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes
}
