const LengthError = 'VRChat chatbox text must be 144 characters or fewer.'

/** Reuse the native error strip without adding another notice or stylesheet. */
export function createChatboxOverflow(): (overflow: boolean) => void {
  let restore: (() => void) | null = null
  let current: HTMLElement | null = null

  return (overflow) => {
    const error = document.getElementById('keyboard-voice-inline-error')
    if (!overflow || error !== current) {
      restore?.()
      restore = null
      current = null
    }
    if (!overflow || error == null) return

    const wrap = document.getElementById('keyboard-composer-wrap')
    const status = document.getElementById('keyboard-composer-status')
    if (current == null || error.textContent !== LengthError) {
      const text = error.textContent ?? ''
      const title = error.title
      const hidden = error.hidden
      const statusHidden = status?.hidden ?? true
      const hadError = wrap?.classList.contains('voice-error') ?? false
      restore = () => {
        if (error.textContent !== LengthError) return
        error.textContent = text
        error.title = title
        error.hidden = hidden
        wrap?.classList.toggle('voice-error', hadError)
        if (status != null) status.hidden = statusHidden
      }
      current = error
    }
    if (error.textContent !== LengthError) error.textContent = LengthError
    error.title = LengthError
    if (error.hidden) error.hidden = false
    wrap?.classList.add('voice-error')
    if (status != null && !status.hidden) status.hidden = true
  }
}
