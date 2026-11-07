import type { SwitchIconView } from './ime-view'
import { animateKey } from './key-animation'
import { requestSwitch } from './native-bridge'
import { ensurePageStyle } from './page-style'

const SwitchStyleId = 'keyboard-ime-switch-style'
const SwitchClass = 'ime-switch-key'
const SpaceId = 'key-SPACE'
/** The key whose look the switch copies, because both are modifier-like keys. */
const ModelKeyId = 'key-LALT'
const ContainerId = 'keyboard-container'
const VisualTransitions = 'background-color, color, box-shadow, scale'

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
  width: 32px; height: auto; object-fit: contain; image-rendering: auto; pointer-events: none;
}
`

export interface SwitchKey {
  /** Draws the IME's icon on the key, and keeps it after the page rebuilds. */
  show: (view: SwitchIconView | null) => void
  /** Gives the switch space beside left Alt, or restores the space bar. */
  setActive: (active: boolean) => void
}

/**
 * A native 1u key after left Alt, paid for by shortening the space bar. See ADR 0046.
 */
export function createSwitchKey(): SwitchKey {
  let icon: SwitchIconView | null = null
  let active = false
  let mounted: {
    key: HTMLElement
    space: HTMLElement
    width: string
    transition: string
  } | null = null

  function show(view: SwitchIconView | null): void {
    icon = view != null && view.width > 0 && view.height > 0 ? view : null
    sync()
  }

  function setActive(value: boolean): void {
    active = value
    sync()
  }

  function sync(): void {
    if (!active || icon == null) {
      unmount()
      return
    }

    mount()
  }

  ensurePageStyle(SwitchStyleId, SwitchStyle)
  observeRebuilds()
  return { show, setActive }

  function mount(): void {
    if (!active || icon == null) {
      return
    }

    const space = document.getElementById(SpaceId)
    const model = document.getElementById(ModelKeyId)
    if (space == null || model == null || space.parentElement !== model.parentElement) {
      return
    }

    if (mounted?.space === space && mounted.key.isConnected) {
      draw(mounted.key, icon)
      return
    }

    unmount()
    const unit = document.getElementById('key-F1')
    if (unit == null) return
    const key = createKey(model, unit)
    draw(key, icon)
    mounted = {
      key,
      space,
      width: space.style.width,
      transition: space.style.transitionProperty,
    }
    // CSS width excludes hover scale; the geometry must change without a transition.
    const spaceWidth = Number.parseFloat(getComputedStyle(space).width)
    space.style.transitionProperty = VisualTransitions
    model.insertAdjacentElement('afterend', key)
    const style = getComputedStyle(key)
    space.style.width = `${spaceWidth - Number.parseFloat(style.width) - Number.parseFloat(style.marginLeft) - Number.parseFloat(style.marginRight)}px`
  }

  /** Restores geometry before restoring transitions, so hiding cannot widen a frame. */
  function unmount(): void {
    if (mounted == null) return
    const { key, space, width, transition } = mounted
    key.remove()
    space.style.width = width
    void space.offsetWidth
    space.style.transitionProperty = transition
    mounted = null
  }

  function observeRebuilds(): void {
    const observer = new MutationObserver(sync)
    observer.observe(document.documentElement, { childList: true, subtree: true })
  }
}

/**
 * Takes the look of the key beside it: same size, margins, and the classes that decide
 * the colours, so a theme change reaches this key too.
 */
function createKey(model: HTMLElement, unit: HTMLElement): HTMLElement {
  // A div rather than a button: the page's focus and active styles would flash a colour.
  const key = document.createElement('div')
  for (const name of model.classList) {
    if (name !== 'active' && name !== 'pointer-hover') {
      key.classList.add(name)
    }
  }

  key.classList.add(SwitchClass)
  key.style.cssText = model.style.cssText
  Object.assign(key.style, {
    width: unit.style.width,
    height: unit.style.height,
    flex: '0 0 auto',
    transitionProperty: VisualTransitions,
    pointerEvents: 'auto',
  })

  const canvas = document.createElement('canvas')
  key.append(canvas)

  key.addEventListener('pointerdown', (event) => {
    event.preventDefault()
    animateKey(key)
    playTap()
    requestSwitch()
  })

  return key
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

function draw(key: HTMLElement, view: SwitchIconView): void {
  const canvas = key.querySelector('canvas')
  if (canvas == null) {
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
