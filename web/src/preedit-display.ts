import styles from './preedit-display.css'
import { ensurePageStyle } from './page-style'

/** One text run and an independent caret preserve shaping across caret positions. */
export function createPreeditDisplay(): {
  show: (composer: HTMLInputElement, text: string, caret: number) => void
  clear: () => void
} {
  ensurePageStyle('keyboard-ime-preedit-style', styles)
  const tab = document.createElement('div')
  tab.id = 'keyboard-ime-preedit'
  tab.hidden = true
  tab.setAttribute('aria-hidden', 'true')
  const viewport = document.createElement('div')
  viewport.className = 'ime-preedit-viewport'
  const content = document.createElement('span')
  content.className = 'ime-preedit-content'
  const run = document.createTextNode('')
  const caret = document.createElement('span')
  caret.className = 'ime-preedit-caret'
  content.append(run, caret)
  viewport.append(content)
  tab.append(viewport)

  return {
    show(composer, text, position) {
      const wrap = composer.closest<HTMLElement>('#keyboard-composer-wrap')
      if (wrap == null || text === '') {
        tab.hidden = true
        return
      }
      if (tab.parentElement !== wrap) wrap.append(tab)
      tab.hidden = false
      run.data = text
      const range = document.createRange()
      const index = Math.max(0, Math.min(text.length, position))
      range.setStart(run, index)
      range.collapse(true)
      const origin = content.getBoundingClientRect()
      const rect = range.getBoundingClientRect()
      const scale = origin.width / content.offsetWidth || 1
      const offset = (rect.left - origin.left) / scale
      caret.style.left = `${offset}px`
      if (offset < viewport.scrollLeft) viewport.scrollLeft = offset
      else if (offset + 1 > viewport.scrollLeft + viewport.clientWidth)
        viewport.scrollLeft = offset + 1 - viewport.clientWidth
    },
    clear() {
      tab.hidden = true
      run.data = ''
      viewport.scrollLeft = 0
    },
  }
}
