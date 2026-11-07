import { requestReturnFeedback } from './native-bridge'

interface NativeKeyboardAudio {
  createKeyboardAudio: (options: {
    getSettings: () => { KeyboardTypingSounds: boolean; KeyboardSoundpackIndex: number }
    getConfiguration: () => { soundPacks: string[] }
  }) => {
    update: () => void
    play: (key: { audio: string }) => void
    dispose: () => void
  }
}

export interface ReturnSound {
  soundPack: string | null
  audio: string
  enabled: boolean
}

/** Uses the native audio module instead of approximating its sound-pack settings. */
export function createReturnFeedback(): {
  press: (key: HTMLElement, hapticTarget: string) => void
  sound: (view: ReturnSound) => void
} {
  let current: ReturnSound = { soundPack: null, audio: 'mute', enabled: false }
  let audio: ReturnType<NativeKeyboardAudio['createKeyboardAudio']> | undefined
  let loading: Promise<NativeKeyboardAudio> | undefined
  let disposed = false

  async function play(view: ReturnSound): Promise<void> {
    current = view
    if (!view.enabled || view.soundPack == null || view.audio === 'mute') return
    const moduleUrl = '/UserInterface/Default/Keyboard/js/keyboard-audio.js'
    loading ??= import(moduleUrl) as Promise<NativeKeyboardAudio>
    const native = await loading
    if (disposed) return
    audio ??= native.createKeyboardAudio({
      getSettings: () => ({
        KeyboardTypingSounds: current.enabled,
        KeyboardSoundpackIndex: 0,
      }),
      getConfiguration: () => ({
        soundPacks: current.soundPack == null ? [] : [current.soundPack],
      }),
    })
    audio.update()
    audio.play({ audio: view.audio })
  }

  window.addEventListener('pagehide', () => {
    disposed = true
    audio?.dispose()
  })
  return {
    press: (key, hapticTarget) => {
      requestReturnFeedback(key.closest('#keyboard-numpad') != null, hapticTarget)
    },
    sound: (view) => {
      void play(view).catch((error) =>
        console.warn('[xso-ime] Return audio failed', error),
      )
    },
  }
}
