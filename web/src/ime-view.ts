/**
 * One composition as the plugin reports it: the codes the IME has not committed,
 * the candidate page in front of them, and the IME name for the bar's chip.
 */
export interface CompositionView {
  preedit: string
  candidates: string[]
  selected: number
  ime: string
}

/**
 * The IME's input-mode icon as the plugin read it: straight RGBA, base64 encoded, at
 * the size the language bar draws it. The switch button scales it up itself.
 */
export interface SwitchIconView {
  width: number
  height: number
  rgba: string
}
