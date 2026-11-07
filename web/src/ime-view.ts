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
 * The composer's own state: whether it holds the focus, the IME the plugin composes
 * with while it does, and the IMEs this language offers. Nothing about another program
 * reaches this view. See ADR 0035 and ADR 0039.
 */
export interface ComposerStateView {
  active: boolean
  ime: string
  /** Names in the order the plugin activates them. */
  profiles: string[]
  /** The position of the active name, or -1 when the plugin cannot say. */
  activeProfile: number
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
