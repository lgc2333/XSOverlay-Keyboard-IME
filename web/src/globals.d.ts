import type { nativeKeyOperations } from './native-key-operation'
import type { insertText } from './composer'
import type { ComposerStateView, CompositionView, SwitchIconView } from './ime-view'
import type { ReturnSound } from './return-feedback'
import type { installComposerAdmission } from './composer-admission'

declare global {
  interface Window {
    /** Injected by Vuplex into every page it hosts. */
    vuplex?: VuplexHost
    /** The functions the plugin calls through ExecuteJavaScript. */
    __xsoKeyboardIme?: KeyboardPageApi
  }
}

interface VuplexHost {
  postMessage: (message: string) => void
}

interface KeyboardPageApi extends ReturnType<typeof installComposerAdmission>, ReturnType<typeof nativeKeyOperations> {
  refreshChatbox: (revision: number, success?: boolean) => void
  setKeyboardFormat: (format: string | null) => void
  resolveReturn: (id: number, taken: boolean) => void
  playReturnSound: (view: ReturnSound) => void
  insertText: typeof insertText
  showComposition: (view: CompositionView) => void
  showSwitchIcon: (view: SwitchIconView | null) => void
  showComposerState: (view: ComposerStateView) => void
}

export {}
