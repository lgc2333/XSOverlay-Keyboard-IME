import type { SwitchIconView } from './ime-view'
import { requestSwitch } from './native-bridge'
import { ensurePageStyle } from './page-style'

const SwitchStyleId = 'keyboard-ime-switch-style'
const SwitchClass = 'ime-switch-key'
const SpaceId = 'key-SPACE'
/** The key whose look the switch copies, because both are modifier-like keys. */
const ModelKeyId = 'key-LALT'
const ContainerId = 'keyboard-container'

/** The page takes its key animation length from this property; the fallback matches. */
const AnimationProperty = '--animation-time'
const AnimationFallbackMilliseconds = 150

/** The settings file the page loads beside each sound; it carries volume and pitch. */
const SoundEntryPattern =
  /\/keyboard-sounds\/[^/]+\/(?:soundpackSettings\.json|[a-z]+\.ogg)$/
const soundPacks = new Map<string, SoundPack>()
const buffers = new Map<string, AudioBuffer>()
let audioContext: AudioContext | null = null

const SwitchStyle = `
#${ContainerId} .${SwitchClass} {
  display: flex; align-items: center; justify-content: center;
  padding: 0; box-sizing: border-box;
  cursor: pointer; -webkit-user-select: none; user-select: none;
  outline: none; -webkit-tap-highlight-color: transparent;
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
  /** Draws the key over the space bar, or takes it away. */
  setActive: (active: boolean) => void
}

/**
 * The switch as a key of the keyboard itself, covering the left end of the space bar.
 * It stays out of the row's layout, so the row keeps its length whatever the theme does.
 * It belongs to the composer, so it is drawn only while the composer holds the focus, and
 * put back whenever the page rebuilds the tree. See ADR 0028, ADR 0035 and ADR 0037.
 */
export function createSwitchKey(): SwitchKey {
  let icon: SwitchIconView | null = null
  let active = false

  function show(view: SwitchIconView | null): void {
    icon = view
    const key = document.querySelector<HTMLElement>(`.${SwitchClass}`)
    if (key != null) {
      draw(key, icon)
    }
  }

  function setActive(value: boolean): void {
    active = value
    if (active) {
      mount()
    } else {
      unmount()
    }
  }

  ensurePageStyle(SwitchStyleId, SwitchStyle)
  observeRebuilds()
  return { show, setActive }

  function mount(): void {
    if (!active) {
      return
    }

    const space = document.getElementById(SpaceId)
    const row = space?.parentElement
    if (space == null || row == null || row.querySelector(`.${SwitchClass}`) != null) {
      return
    }

    const unit = Number.parseFloat(space.style.height)
    if (!(unit > 0)) {
      return
    }

    // The key covers the space bar's left end without joining the row's layout, so the
    // row keeps exactly its length and nothing can overflow. See ADR 0037.
    if (getComputedStyle(row).position === 'static') {
      row.style.position = 'relative'
    }

    const key = createKey(space, unit)
    draw(key, icon)
    key.style.position = 'absolute'
    key.style.left = `${space.offsetLeft}px`
    key.style.top = `${space.offsetTop}px`
    // Positioned by its own corner, so a copied margin would shift it off the space.
    key.style.margin = '0'
    row.appendChild(key)
  }

  /** Takes the key out; the space bar never changed, so nothing is given back. */
  function unmount(): void {
    document.querySelector<HTMLElement>(`.${SwitchClass}`)?.remove()
  }

  function observeRebuilds(): void {
    const observer = new MutationObserver(mount)
    observer.observe(document.documentElement, { childList: true, subtree: true })
  }
}

/**
 * Takes the look of the key beside it: same size, margins, and the classes that decide
 * the colours, so a theme change reaches this key too.
 */
function createKey(space: HTMLElement, unit: number): HTMLElement {
  // A div rather than a button: the page's focus and active styles would flash a colour.
  const key = document.createElement('div')
  const model = document.getElementById(ModelKeyId) ?? space
  for (const name of model.classList) {
    if (name !== 'active') {
      key.classList.add(name)
    }
  }

  key.classList.add(SwitchClass)
  Object.assign(key.style, {
    width: `${unit}px`,
    height: space.style.height,
    margin: space.style.margin,
    marginTop: space.style.marginTop,
    backgroundColor: model.style.backgroundColor,
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
    animate(key)
    playTap()
    requestSwitch()
  })

  return key
}

/**
 * Repeats the press animation the page plays on its own keys, so this key moves alike.
 */
function animate(key: HTMLElement): void {
  if (typeof key.animate !== 'function') {
    return
  }

  key.animate([{ scale: '1.05' }, { scale: '0.95' }, { scale: '1.05' }], {
    duration: animationDuration(),
    easing: 'linear',
    fill: 'none',
  })
}

/** Reads the same property the page's own keys read, so the length cannot drift. */
function animationDuration(): number {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(AnimationProperty)
    .trim()
  const match = /^([\d.]+)\s*(ms|s)?$/i.exec(value)
  const number = match == null ? Number.NaN : Number(match[1])
  if (!Number.isFinite(number)) {
    return AnimationFallbackMilliseconds
  }

  return match?.[2]?.toLowerCase() === 's' ? number * 1000 : number
}

interface SoundPack {
  /** Base URL of the pack, ending with a slash. */
  base: string
  volume: number
  minimumPitch: number
  maximumPitch: number
}

/**
 * Plays the tap sound through WebAudio, the way the page does: the same decoded buffer,
 * the same gain and the same pitch range. A media element sounds different in this
 * webview, so the pipeline has to match rather than just the number.
 */
function playTap(): void {
  void playTapFromPack()
}

async function playTapFromPack(): Promise<void> {
  const base = findSoundPackBase()
  if (base == null) {
    return
  }

  const pack = (await loadSoundPack(base)) ?? {
    base,
    volume: 1,
    minimumPitch: 1,
    maximumPitch: 1,
  }
  const context = getAudioContext()
  if (context == null) {
    return
  }

  if (context.state === 'suspended') {
    void context.resume().catch(() => {})
  }

  const buffer = await loadBuffer(context, `${pack.base}tap.ogg`)
  if (buffer == null) {
    return
  }

  const source = context.createBufferSource()
  const gain = context.createGain()
  source.buffer = buffer
  source.playbackRate.value =
    pack.minimumPitch +
    Math.random() * Math.max(0, pack.maximumPitch - pack.minimumPitch)
  gain.gain.value = clampUnit(pack.volume)
  source.connect(gain)
  gain.connect(context.destination)
  source.start()
}

function getAudioContext(): AudioContext | null {
  if (audioContext != null) {
    return audioContext
  }

  try {
    audioContext = new AudioContext()
  } catch {
    audioContext = null
  }

  return audioContext
}

/** Decodes each file once, the way the page caches its pack. */
async function loadBuffer(
  context: AudioContext,
  url: string,
): Promise<AudioBuffer | null> {
  const cached = buffers.get(url)
  if (cached != null) {
    return cached
  }

  try {
    const response = await fetch(url)
    if (!response.ok) {
      return null
    }

    const buffer = await context.decodeAudioData(await response.arrayBuffer())
    buffers.set(url, buffer)
    return buffer
  } catch {
    return null
  }
}

/** The pack is named only in the requests the page made, so the base comes from there. */
function findSoundPackBase(): string | null {
  for (const entry of performance.getEntriesByType('resource')) {
    if (SoundEntryPattern.test(entry.name)) {
      return entry.name.replace(/[^/]+$/, '')
    }
  }

  return null
}

/** Reads the settings file the page also reads, so volume and pitch match it. */
async function loadSoundPack(base: string): Promise<SoundPack | null> {
  const cached = soundPacks.get(base)
  if (cached != null) {
    return cached
  }

  try {
    const response = await fetch(`${base}soundpackSettings.json`)
    if (!response.ok) {
      return null
    }

    const settings = (await response.json()) as Record<string, unknown>
    const minimumPitch = numberOr(settings.minimumPitchRange, 1)
    const pack: SoundPack = {
      base,
      volume: numberOr(settings.volume, 1),
      minimumPitch,
      maximumPitch: numberOr(settings.maximumPitchRange, minimumPitch),
    }
    soundPacks.set(base, pack)
    return pack
  } catch {
    return null
  }
}

function numberOr(value: unknown, fallback: number): number {
  const number = Number(value)
  return Number.isFinite(number) ? number : fallback
}

function clampUnit(value: number): number {
  return Math.max(0, Math.min(1, value))
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
