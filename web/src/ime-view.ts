/**
 * One composition as the plugin reports it: the codes the IME has not committed and
 * the candidate page in front of them.
 */
export interface CompositionView {
  preedit: string
  candidates: string[]
  selected: number
  /** True while the IME holds a composition, even when it reports no codes. */
  composing: boolean
  /** The caret the IME holds inside the codes, in characters from their start. */
  selectionStart: number
  selectionEnd: number
}

/**
 * The composer's own state: whether it holds the focus, and the IME the plugin composes
 * with while it does. Nothing about another program reaches this view. See ADR 0035.
 */
export interface ComposerStateView {
  active: boolean
  ime: string
}

/**
 * The IME's input-mode icon as the plugin read it: straight RGBA, base64 encoded, at
 * the size the language bar draws it. The switch key scales it up itself.
 */
export interface SwitchIconView {
  width: number
  height: number
  rgba: string
}
