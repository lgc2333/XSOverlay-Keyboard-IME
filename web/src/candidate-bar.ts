import barStyles from './candidate-bar.css'
import type { ComposerStateView, CompositionView } from './ime-view'
import { ensurePageStyle } from './page-style'

const BarId = 'keyboard-ime-bar'
const StyleId = 'keyboard-ime-bar-style'
const ContainerId = 'keyboard-container'
const BackgroundId = 'keyboard-background'

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
 * The page rebuilds the whole keyboard tree on every layout update, so the bar is put
 * back whenever it is not a child of the current container. See ADR 0004.
 */
export function createCandidateBar(): CandidateBar {
  const bar = document.createElement('div')
  bar.id = BarId

  const candidates = document.createElement('div')
  candidates.className = 'ime-candidates'
  const chip = document.createElement('div')
  chip.className = 'ime-chip'
  chip.hidden = true
  bar.append(candidates, chip)

  function render(view: CompositionView | null): void {
    const shown = view?.candidates ?? []
    candidates.replaceChildren(
      ...shown.map((text, index) => createCandidate(text, index, view)),
    )
  }

  function showState(view: ComposerStateView): void {
    chip.hidden = !view.active || view.ime.length === 0
    chip.textContent = view.ime
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
  }

  function observeRebuilds(): void {
    const observer = new MutationObserver(mount)
    observer.observe(document.documentElement, { childList: true, subtree: true })
  }
}
