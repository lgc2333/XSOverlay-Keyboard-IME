/** Adds one page-level style element the first time a module asks for it. */
export function ensurePageStyle(id: string, css: string): void {
  if (document.getElementById(id) != null) {
    return
  }

  const element = document.createElement('style')
  element.id = id
  element.textContent = css
  document.head.appendChild(element)
}
