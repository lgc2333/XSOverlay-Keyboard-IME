import barStyles from './candidate-bar.css'
import type { ComposerStateView, CompositionView } from './ime-view'
import { ensurePageStyle } from './page-style'

const BarId = 'keyboard-ime-bar'
const StyleId = 'keyboard-ime-bar-style'
const ContainerId = 'keyboard-container'
const BackgroundId = 'keyboard-background'
const SelectedClass = 'ime-selected'
const OverflowStartClass = 'ime-overflow-start'
const OverflowEndClass = 'ime-overflow-end'

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

  const chip = document.createElement('div')
  chip.className = 'ime-chip'
  chip.hidden = true
  bar.append(frame, chip)

  /** The candidate the IME has highlighted, or null when it reports no selection. */
  let active: HTMLElement | null = null

  function render(view: CompositionView | null): void {
    const shown = view?.candidates ?? []
    candidates.replaceChildren(
      ...shown.map((text, index) => createCandidate(text, index, view)),
    )
    active = candidates.querySelector<HTMLElement>(`.${SelectedClass}`)
    revealActive()
    updateEdges()
  }

  function showState(view: ComposerStateView): void {
    chip.hidden = !view.active || view.ime.length === 0
    chip.textContent = view.ime
    // The chip takes width from the candidates, so the active one may have moved out.
    revealActive()
    updateEdges()
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
