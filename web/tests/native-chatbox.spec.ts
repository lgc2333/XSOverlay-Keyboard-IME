import { expect, test } from './browser-test'

declare global {
  interface Window {
    nativeChatbox: {
      speech: {
        captureVoiceInsertion: () => void
        applyVoiceTranscript: () => void
        handleVRChatChatboxTextResult: (result: { success: boolean }) => void
      }
      state: { VoiceSessionId: string; VoicePartialTranscript: string }
    }
  }
}

test.beforeEach(async ({ page, request }) => {
  test.skip(!(await request.get('/native-keyboard-available')).ok(), 'Requires installed XSOverlay modules.')
  test.skip(!await (await request.get('/native-keyboard-available')).json(), 'Requires installed XSOverlay modules.')
  await page.goto('/')
  await page.evaluate(async () => {
    const prefix = '/apps/UserInterface/Default/Keyboard/js/'
    const { createKeyboardState } = await import(`${prefix}keyboard-state.js`)
    const { createKeyboardSpeechComposer } = await import(`${prefix}keyboard-speech-composer.js`)
    const { createKeyboardSpeechView } = await import(`${prefix}keyboard-speech-view.js`)
    const state = createKeyboardState()
    state.Configuration = { vrChatActive: true, vrChatOscAvailable: true }
    state.VrcComposerDestination = true
    const container = document.getElementById('keyboard-container')!
    container.replaceChildren()
    const toggle = document.createElement('button')
    toggle.id = 'keyboard-toolbar-button-vrc'
    state.VrcToggle = toggle
    container.append(toggle)
    let updateToolbar = (): void => {}
    const speech = createKeyboardSpeechComposer({
      state,
      commands: { SendVRChatChatboxText: 'SendVRChatChatboxText' },
      send: (command: string, json: string) => window.messages.push(`${command}:${json}`),
      releaseKeyboardInputFocus: () => document.getElementById('keyboard-voice-input')!.blur(),
      updateToolbar: () => updateToolbar(),
    })
    const view = createKeyboardSpeechView({
      state,
      createControl: (id: string) => Object.assign(document.createElement('button'), { id, textContent: id }),
      bindPress: (button: HTMLButtonElement, callback: () => void) => button.addEventListener('click', callback),
      bindLocalizedTooltip: () => {},
      isVRChatActive: speech.isVRChatActive,
      isVRChatOscAvailable: speech.isVRChatOscAvailable,
      sendComposerText: speech.sendComposerText,
      clearComposer: speech.clearComposer,
    })
    updateToolbar = view.updateToolbar
    view.render(container)
    window.nativeChatbox = { speech, state }
  })
  await page.locator('#keyboard-voice-input').focus()
})

test('native provisional speech reaches the broadcast bridge without input events', async ({ page }) => {
  await page.evaluate(() => {
    const { speech, state } = window.nativeChatbox
    speech.captureVoiceInsertion()
    state.VoiceSessionId = 'test-voice-session'
    state.VoicePartialTranscript = 'provisional voice'
    speech.applyVoiceTranscript()
  })
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('provisional voice')
  await expect.poll(() => page.evaluate(() => window.messages.some(message =>
    message.startsWith('xso-keyboard-ime chatbox ') && JSON.parse(message.slice('xso-keyboard-ime chatbox '.length)).text === 'provisional voice',
  ))).toBe(true)
})

test('native Enter clears only after success and original overflow rejection remains intact', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('native formal send')
  await page.keyboard.press('Enter')
  const id = await page.evaluate(() => Number(window.messages.filter(message => message.startsWith('xso-keyboard-ime return ')).at(-1)!.split(' ').at(-1)))
  await page.evaluate(id => window.__xsoKeyboardIme!.resolveReturn(id, false), id)
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('native formal send')
  await expect(page.locator('#keyboard-voice-input')).toHaveAttribute('aria-busy', 'true')
  expect(await page.evaluate(() => window.messages.filter(message => message.startsWith('SendVRChatChatboxText:')))).toEqual([
    'SendVRChatChatboxText:{"text":"native formal send"}',
  ])
  await page.evaluate(() => window.nativeChatbox.speech.handleVRChatChatboxTextResult({ success: true }))
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('')
  await page.locator('#keyboard-voice-input').fill('a'.repeat(145))
  await page.keyboard.press('Enter')
  const next = await page.evaluate(() => Number(window.messages.filter(message => message.startsWith('xso-keyboard-ime return ')).at(-1)!.split(' ').at(-1)))
  await page.evaluate(id => window.__xsoKeyboardIme!.resolveReturn(id, false), next)
  await expect(page.locator('#keyboard-voice-inline-error')).toHaveText('VRChat chatbox text must be 144 characters or fewer.')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('a'.repeat(145))
  await expect(page.locator('#keyboard-voice-input')).toBeFocused()
  expect(await page.evaluate(() => window.messages.filter(message => message.startsWith('SendVRChatChatboxText:')).length)).toBe(1)
})
