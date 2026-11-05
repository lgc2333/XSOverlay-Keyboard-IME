import type { insertText } from './composer'

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

interface KeyboardPageApi {
  insertText: typeof insertText
}

export {}
