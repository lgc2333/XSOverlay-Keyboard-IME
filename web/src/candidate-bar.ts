import type { CompositionView } from './ime-view'
import { ensurePageStyle } from './page-style'

const BarId = 'keyboard-ime-bar'
const StyleId = 'keyboard-ime-bar-style'
const ContainerId = 'keyboard-container'
const BackgroundId = 'keyboard-background'

const BarStyle = `
#${BarId} {
  display: flex; align-items: center; gap: 8px; align-self: stretch;
  height: 64px; padding: var(--keyboard-toolbar-inset); box-sizing: border-box;
  border-radius: var(--keyboard-panel-radius);
  background: var(--theme-dark); box-shadow: 0 0 4px rgba(0,0,0,.5);
  font: 20px 'Roboto Flex', 'Noto Sans', sans-serif;
  color: var(--theme-contrasting); z-index: 120;
}
#${BarId} .ime-candidates { display: flex; gap: 8px; flex: 1 1 auto; min-width: 0; overflow: hidden; }
#${BarId} .ime-candidate {
  display: flex; align-items: center; gap: 8px; flex: 0 0 auto;
  height: var(--keyboard-control-size); padding: 0 12px;
  border-radius: var(--keyboard-control-radius);
  background: var(--theme-mid); white-space: nowrap;
  box-shadow: inset 0 0 0 0 var(--theme-accent);
  transition: background var(--keyboard-animation-time), box-shadow var(--keyboard-animation-time);
}
#${BarId} .ime-candidate .ime-number { font-weight: 700; color: #8b98a3; }
#${BarId} .ime-candidate.ime-selected {
  background: var(--theme-hi); box-shadow: inset 0 0 0 2px var(--theme-accent);
}
#${BarId} .ime-candidate.ime-selected .ime-number { color: #d8e6ea; }
#${BarId} .ime-candidate.ime-selected .ime-text { font-weight: 600; }
#${BarId} .ime-chip {
  display: flex; align-items: center; flex: 0 0 auto;
  height: var(--keyboard-control-size); padding: 0 12px;
  border-radius: var(--keyboard-control-radius); background: var(--theme-mid);
  white-space: nowrap;
}
`

export interface CandidateBar {
  render: (view: CompositionView | null) => void
}

/**
 * The candidate bar: one row between the toolbar and the keys, owned by this script.
 * It shows the candidates and the IME name. See ADR 0004 and ADR 0018.
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
  bar.append(candidates, chip)

  function render(view: CompositionView | null): void {
    const shown = view?.candidates ?? []
    candidates.replaceChildren(
      ...shown.map((text, index) => createCandidate(text, index, view)),
    )
    chip.hidden = view == null || view.ime.length === 0
    chip.textContent = view?.ime ?? ''
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
  return { render }

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
