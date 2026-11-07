import barStyles from './candidate-bar.css'
import type { ComposerStateView, CompositionView } from './ime-view'
import { requestProfile } from './native-bridge'
import { ensurePageStyle } from './page-style'

const BarId = 'keyboard-ime-bar'
const StyleId = 'keyboard-ime-bar-style'
const ContainerId = 'keyboard-container'
const BackgroundId = 'keyboard-background'
const SelectedClass = 'ime-selected'
const OverflowStartClass = 'ime-overflow-start'
const OverflowEndClass = 'ime-overflow-end'
const CurrentProfileClass = 'ime-profile-current'
const BusyClass = 'ime-chip-busy'

/** How long the chip stays busy when the plugin sends no answer at all. */
const BusySettleMilliseconds = 2000

/** The page's ray only hits these classes, so the clickable parts borrow them. */
const RayButtonClass = 'dropdown-button'
const RayItemClass = 'dropdown-item'

/** The width of the side fade; the reveal keeps the active candidate clear of it. */
const EdgeFadeWidth = 32

/** The stylesheet names the bar by a placeholder, so the id lives in one place. */
const BarStyle = barStyles.replaceAll('#bar-id', `#${BarId}`)

export interface CandidateBar {
  render: (view: CompositionView | null) => void
  showState: (view: ComposerStateView) => void
}

/**
 * The candidate bar: one row between the toolbar and the keys, owned by this script.
 * It shows the candidates, and the IME this thread composes with while the composer
 * holds the focus. See ADR 0004, ADR 0018 and ADR 0035.
 * The name opens the language's profiles and picking one asks the plugin to activate it.
 * See ADR 0039.
 * The active candidate is scrolled just far enough into view, never centred, and only
 * a side that still hides candidates fades. See ADR 0018.
 * The page rebuilds the whole keyboard tree on every layout update, so the bar is put
 * back whenever it is not a child of the current container. See ADR 0004.
 */
export function createCandidateBar(): CandidateBar {
  const bar = document.createElement('div')
  bar.id = BarId
  // The stylesheet reads the fade width from here, so it lives in one place.
  bar.style.setProperty('--ime-edge-fade-width', `${EdgeFadeWidth}px`)

  const frame = document.createElement('div')
  frame.className = 'ime-candidates-frame'

  const candidates = document.createElement('div')
  candidates.className = 'ime-candidates'

  const startFade = document.createElement('div')
  startFade.className = 'ime-edge-fade ime-edge-fade-start'
  startFade.setAttribute('aria-hidden', 'true')

  const endFade = document.createElement('div')
  endFade.className = 'ime-edge-fade ime-edge-fade-end'
  endFade.setAttribute('aria-hidden', 'true')

  frame.append(candidates, startFade, endFade)

  const chipWrap = document.createElement('div')
  chipWrap.className = 'ime-chip-wrap'

  const chip = document.createElement('div')
  chip.className = 'ime-chip'
  chip.hidden = true

  const menu = document.createElement('div')
  menu.className = 'ime-profiles'
  menu.hidden = true
  menu.setAttribute('role', 'menu')

  chipWrap.append(chip, menu)
  bar.append(frame, chipWrap)

  /** The candidate the IME has highlighted, or null when it reports no selection. */
  let active: HTMLElement | null = null

  /** The IME name and the profiles the last state carried, in activation order. */
  let ime = ''
  let profiles: string[] = []
  let activeProfile = -1
  let profileSignature = ''
  let busyTimer: number | null = null
  let menuOpen = false

  function render(view: CompositionView | null): void {
    const shown = view?.candidates ?? []
    candidates.replaceChildren(
      ...shown.map((text, index) => createCandidate(text, index, view)),
    )
    active = candidates.querySelector<HTMLElement>(`.${SelectedClass}`)
    revealActive()
    updateEdges()
  }

  /** A pick takes a round trip, so the chip shows it is working until the state arrives. */
  function markBusy(): void {
    chip.classList.add(BusyClass)
    chip.setAttribute('aria-busy', 'true')
    if (busyTimer != null) {
      window.clearTimeout(busyTimer)
    }

    busyTimer = window.setTimeout(clearBusy, BusySettleMilliseconds)
  }

  function clearBusy(): void {
    if (busyTimer != null) {
      window.clearTimeout(busyTimer)
      busyTimer = null
    }

    chip.classList.remove(BusyClass)
    chip.removeAttribute('aria-busy')
  }

  function showState(view: ComposerStateView): void {
    clearBusy()
    ime = view.ime
    profiles = view.profiles
    activeProfile = view.activeProfile

    const shown = view.active && ime.length > 0
    const clickable = shown && profiles.length > 0
    chip.hidden = !shown
    chip.textContent = ime
    chip.classList.toggle(RayButtonClass, clickable)
    if (clickable) {
      chip.setAttribute('aria-haspopup', 'menu')
      chip.setAttribute('aria-expanded', menuOpen ? 'true' : 'false')
      // The page's dropdown class brings its own arrow; ours is drawn in the stylesheet.
      refreshProfiles()
    } else {
      closeMenu()
      chip.removeAttribute('aria-haspopup')
      chip.removeAttribute('aria-expanded')
    }

    // The chip takes width from the candidates, so the active one may have moved out.
    revealActive()
    updateEdges()
  }

  /** Rebuilds the items only when the list or the current position changed. */
  function refreshProfiles(): void {
    const signature = `${activeProfile}\u0000${profiles.join('\u0000')}`
    if (signature === profileSignature) {
      return
    }

    profileSignature = signature
    menu.replaceChildren(...profiles.map(createProfile))
  }

  function createProfile(name: string, index: number): HTMLElement {
    const item = document.createElement('div')
    item.className = `ime-profile ${RayItemClass}`
    item.setAttribute('role', 'menuitem')

    const label = document.createElement('span')
    label.className = 'ime-profile-name'
    label.textContent = name
    item.append(label)

    // The position the plugin sent decides, so two IMEs with one name stay distinct.
    if (index === activeProfile) {
      item.classList.add(CurrentProfileClass)
      const check = document.createElement('span')
      check.className = 'ime-profile-check'
      check.textContent = '✓'
      item.append(check)
    }

    // Pointerdown, so the press cannot move the focus off the composer. See ADR 0035.
    item.addEventListener('pointerdown', (event) => {
      event.preventDefault()
      event.stopPropagation()
      closeMenu()
      markBusy()
      requestProfile(index)
    })

    return item
  }

  function openMenu(): void {
    if (chip.hidden || profiles.length === 0) {
      return
    }

    menuOpen = true
    menu.hidden = false
    chip.setAttribute('aria-expanded', 'true')
  }

  function closeMenu(): void {
    if (!menuOpen) {
      return
    }

    menuOpen = false
    menu.hidden = true
    chip.setAttribute('aria-expanded', 'false')
  }

  function toggleMenu(): void {
    if (menuOpen) {
      closeMenu()
    } else {
      openMenu()
    }
  }

  /** Scrolls the shortest way that shows the active candidate; it stays where it is. */
  function revealActive(): void {
    if (active == null || candidates.clientWidth === 0) {
      return
    }

    // Layout coordinates, so a scaled page does not skew the amount scrolled. A candidate
    // clear of the fades stays put; one the fades or the edge cut into is scrolled in by the
    // shortest amount that shows all of it.
    const itemStart = active.offsetLeft
    const itemEnd = itemStart + active.offsetWidth
    const visibleStart = candidates.scrollLeft
    const visibleEnd = visibleStart + candidates.clientWidth
    const start = itemStart - EdgeFadeWidth
    const end = itemEnd + EdgeFadeWidth
    if (start < visibleStart) {
      candidates.scrollLeft = start
    } else if (end > visibleEnd) {
      // A candidate wider than the list keeps its start in view.
      candidates.scrollLeft = Math.max(start, end - candidates.clientWidth)
    }
  }

  /** Marks each side that still hides candidates, so only that side fades. */
  function updateEdges(): void {
    const max = candidates.scrollWidth - candidates.clientWidth
    const atEnd = max > 0 && candidates.scrollLeft >= max - 0.5
    bar.classList.toggle(OverflowStartClass, candidates.scrollLeft > 0.5)
    bar.classList.toggle(OverflowEndClass, max > 0 && !atEnd)
  }

  function createCandidate(
    text: string,
    index: number,
    view: CompositionView | null,
  ): HTMLElement {
    const element = document.createElement('div')
    element.className = 'ime-candidate'
    element.classList.toggle(SelectedClass, view != null && index === view.selected)

    const number = document.createElement('span')
    number.className = 'ime-number'
    number.textContent = String(index + 1)

    const label = document.createElement('span')
    label.className = 'ime-text'
    label.textContent = text

    element.append(number, label)
    return element
  }

  // A press on the name keeps the composer's focus, and opens or closes the list.
  chip.addEventListener('pointerdown', (event) => {
    event.preventDefault()
    event.stopPropagation()
    if (chip.classList.contains(RayButtonClass)) {
      toggleMenu()
    }
  })

  // The panel is a small floating layer: it keeps the press off the page behind it.
  menu.addEventListener('pointerdown', (event) => {
    event.preventDefault()
    event.stopPropagation()
  })

  // A press anywhere else closes the list, before the page handles that press.
  document.addEventListener(
    'pointerdown',
    (event) => {
      const target = event.target
      if (menuOpen && !(target instanceof Node && chipWrap.contains(target))) {
        closeMenu()
      }
    },
    true,
  )

  candidates.addEventListener('scroll', updateEdges, { passive: true })
  const observer = new ResizeObserver(() => {
    revealActive()
    updateEdges()
  })
  observer.observe(candidates)

  ensurePageStyle(StyleId, BarStyle)
  render(null)
  mount()
  observeRebuilds()
  return { render, showState }

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
    // A rebuild keeps the bar and the open list as they are; only the anchor moved.
    revealActive()
    updateEdges()
  }

  function observeRebuilds(): void {
    const mutation = new MutationObserver(mount)
    mutation.observe(document.documentElement, {
      childList: true,
      subtree: true,
    })
  }
}
