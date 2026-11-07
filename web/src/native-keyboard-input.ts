import { keyAnimationDuration } from './key-animation'

export interface NativeRepeatController {
  begin: (id: string, hapticTarget: string) => void
  dispose: () => void
}

export interface NativeKeyboardInput {
  animate: (key: HTMLElement) => void
  createRepeatController: (options: { send: (repeated: boolean) => void }) => NativeRepeatController
  dispose: () => void
}

/** The native controller owns repeat timing and animation cleanup. */
export async function loadNativeKeyboardInput(): Promise<NativeKeyboardInput> {
  const moduleUrl = '/UserInterface/Default/Keyboard/js/keyboard-input.js'
  const native = await import(moduleUrl) as {
    createKeyboardInputController: (options: { getAnimationDuration: () => number }) => NativeKeyboardInput
  }
  return native.createKeyboardInputController({ getAnimationDuration: keyAnimationDuration })
}
