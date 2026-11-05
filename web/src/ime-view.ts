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
