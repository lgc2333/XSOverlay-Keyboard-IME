const Ready = 'xso-keyboard-ime ready'
const Switch = 'xso-keyboard-ime switch'
const Cancel = 'xso-keyboard-ime cancel'
const ComposerActive = 'xso-keyboard-ime composer-active'
const ComposerInactive = 'xso-keyboard-ime composer-inactive'
const Profile = 'xso-keyboard-ime profile '
const CandidateArea = 'xso-keyboard-ime candidate-area '

export function reportChatbox(state: { revision: number; active: boolean; text: string; submitting: boolean }): boolean {
  return post(`xso-keyboard-ime chatbox ${JSON.stringify(state)}`)
}

export function requestReturn(id: number): boolean {
  return post(`xso-keyboard-ime return ${id}`)
}

export function requestReturnFeedback(numpad: boolean, hapticTarget: string): boolean {
  return post(
    `xso-keyboard-ime return-feedback ${JSON.stringify({ numpad, hapticTarget })}`,
  )
}

export interface CandidateAreaBounds {
  left: number
  top: number
  right: number
  bottom: number
  scrollX: number
  scrollY: number
}

/** Reports the whole bar as a ray target, with wheel events aimed at its candidate viewport. */
export function reportCandidateArea(bounds: CandidateAreaBounds | null): boolean {
  return post(`${CandidateArea}${JSON.stringify(bounds)}`)
}

/**
 * Tells the plugin that the page runs the injected script. Vuplex hands the message to
 * C# as an IWebView.MessageEmitted event, so the plugin never polls. See ADR 0005.
 */
export function announceReady(): boolean {
  return post(Ready)
}

/**
 * Asks the plugin to press the IME's own input-mode button, which is how the switch
 * changes 中 and 英 without the plugin writing a mode. See ADR 0028.
 */
export function requestSwitch(): boolean {
  return post(Switch)
}

/**
 * Asks the plugin to cancel the composition the IME holds, because the composer no
 * longer holds the text it was composing into. See ADR 0027.
 */
export function requestCancel(): boolean {
  return post(Cancel)
}

/**
 * Reports that the composer took or gave up the focus, which is the only thing that
 * shows or hides the IME name and the switch. See ADR 0035.
 */
export function reportComposerActive(active: boolean): boolean {
  return post(active ? ComposerActive : ComposerInactive)
}

/**
 * Asks the plugin to activate one of the listed IMEs, by its index in the list the last
 * composer state carried. See ADR 0039.
 */
export function requestProfile(index: number): boolean {
  return post(`${Profile}${index}`)
}

function post(message: string): boolean {
  const host = window.vuplex
  if (host == null) {
    return false
  }

  host.postMessage(message)
  return true
}
