import { expect, test } from './browser-test'

test.beforeEach(async ({ page }) => {
  await page.setContent(
    '<div id="keyboard-composer-wrap"><span id="keyboard-voice-inline-error" hidden></span><input id="keyboard-voice-input"><button id="keyboard-voice-send">Send</button></div><button id="key-RETURN">Return</button><div id="keyboard-background"></div>',
  )
  await page.evaluate(() => {
    window.messages = []
    window.vuplex = { postMessage: (message) => window.messages.push(message) }
  })
  await page.addScriptTag({ path: 'dist/keyboard-ime.js' })
  await page.evaluate(() => {
    const api = window.__xsoKeyboardIme!
    api.bindFocus('document-a', 1, api.focusDocument)
    api.applyFocus({ kind: 'clear', document: 'document-a', revision: 1, request: 0 })
    api.applyFocus({ kind: 'open', document: 'document-a', revision: 1, request: 0 })
  })
  await page.locator('#keyboard-voice-input').click()
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'held',
      document: 'document-a',
      revision: 1,
      request: 1,
    }),
  )
  await page.addScriptTag({
    content: `
      const input = document.getElementById('keyboard-voice-input')
      const send = document.getElementById('keyboard-voice-send')
      const submit = () => send.dispatchEvent(new PointerEvent('pointerdown', {
        bubbles: true, cancelable: true, button: 0, buttons: 1, pointerId: 1, pointerType: 'mouse'
      }))
      send.addEventListener('pointerdown', event => {
        event.preventDefault()
        input.blur()
        window.messages.push('native-submit')
      })
      input.addEventListener('keydown', event => { if (event.key === 'Enter') submit() })
      for (const type of ['pointerdown', 'touchstart']) {
        document.getElementById('key-RETURN').addEventListener(type, () => {
          submit()
          window.messages.push('native-return-feedback')
        })
      }
    `,
  })
})

test('replays the native send after the host restores the old focus target', async ({
  page,
}) => {
  await page.locator('#keyboard-voice-send').click()
  expect(await page.evaluate(() => window.messages)).not.toContain('native-submit')
  expect(
    await page.evaluate(() =>
      window.messages.some((message) => message.includes('"kind":"submit"')),
    ),
  ).toBe(true)

  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'clear',
      document: 'document-a',
      revision: 2,
      request: 0,
      restored: true,
    }),
  )
  expect(
    await page.evaluate(() =>
      window.messages.filter((message) => message === 'native-submit'),
    ),
  ).toHaveLength(1)
})

test('shows a short error and drops the send when the old focus cannot be restored', async ({
  page,
}) => {
  await page.locator('#keyboard-voice-send').click()
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'clear',
      document: 'document-a',
      revision: 2,
      request: 0,
      restored: false,
    }),
  )
  expect(await page.evaluate(() => window.messages)).not.toContain('native-submit')
  await expect(page.locator('#keyboard-voice-inline-error')).toHaveText(
    'Send failed. Try again.',
  )
  await expect(page.locator('#keyboard-voice-inline-error')).toBeVisible()
})

test('defers the page Enter send through the same handshake', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('draft')
  await page.keyboard.press('Enter')
  const returnId = await page.evaluate(() =>
    Number(
      JSON.parse(
        window.messages
          .find(
            (message) =>
              message.startsWith('xso-keyboard-ime focus ') &&
              JSON.parse(message.slice('xso-keyboard-ime focus '.length)).kind ===
                'return',
          )!
          .slice('xso-keyboard-ime focus '.length),
      ).index,
    ),
  )
  await page.evaluate(
    (id) => window.__xsoKeyboardIme!.resolveReturn(id, false),
    returnId,
  )
  expect(await page.evaluate(() => window.messages)).not.toContain('native-submit')
  expect(
    await page.evaluate(() =>
      window.messages.some((message) => message.includes('"kind":"submit"')),
    ),
  ).toBe(true)

  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'clear',
      document: 'document-a',
      revision: 2,
      request: 0,
      restored: true,
    }),
  )
  expect(
    await page.evaluate(() =>
      window.messages.filter((message) => message === 'native-submit'),
    ),
  ).toHaveLength(1)
})

for (const inputType of ['pointer', 'touch']) {
  for (const restored of [true, false]) {
    test(`${inputType} Return has one feedback request when restore ${restored ? 'succeeds' : 'fails'}`, async ({ page }) => {
      const composer = page.locator('#keyboard-voice-input')
      const key = page.locator('#key-RETURN')
      await composer.fill('draft')
      if (inputType === 'pointer') {
        await key.dispatchEvent('pointerdown', { button: 0, buttons: 1, pointerId: 7, pointerType: 'mouse' })
      } else {
        await key.evaluate((target) => {
          const touch = new Touch({ identifier: 1, target })
          target.dispatchEvent(new TouchEvent('touchstart', {
            bubbles: true,
            cancelable: true,
            changedTouches: [touch],
            touches: [touch],
            targetTouches: [touch],
          }))
        })
      }
      const waiting = await page.evaluate(() => {
        const prefix = 'xso-keyboard-ime focus '
        const request = window.messages
          .filter((message) => message.startsWith(prefix))
          .map((message) => JSON.parse(message.slice(prefix.length)))
          .find((message) => message.kind === 'return')
        window.__xsoKeyboardIme!.resolveReturn(request.index, false)
        return [...window.messages]
      })
      expect(waiting).not.toContain('native-submit')
      expect(waiting).not.toContain('native-return-feedback')
      expect(waiting.some((message) => message.includes('"kind":"submit"'))).toBe(true)
      expect(waiting.some((message) => message.startsWith('xso-keyboard-ime return-feedback '))).toBe(false)

      await page.evaluate((restored) => {
        const clear = { kind: 'clear', document: 'document-a', revision: 2, request: 0, restored }
        window.__xsoKeyboardIme!.applyFocus(clear)
        window.__xsoKeyboardIme!.applyFocus(clear)
      }, restored)
      const messages = await page.evaluate(() => window.messages)
      expect(messages.filter((message) => message === 'native-submit')).toHaveLength(restored ? 1 : 0)
      expect(messages.filter((message) => message === 'native-return-feedback')).toHaveLength(restored ? 1 : 0)
      const pluginFeedback = messages.filter((message) => message.startsWith('xso-keyboard-ime return-feedback '))
      const expectedTarget = inputType === 'pointer' ? 'keyboard' : 'leftcontroller'
      expect(pluginFeedback).toEqual(restored ? [] : [
        `xso-keyboard-ime return-feedback ${JSON.stringify({ numpad: false, hapticTarget: expectedTarget })}`,
      ])
      await expect(composer).not.toBeFocused()
      if (!restored) {
        await expect(composer).toHaveValue('draft')
        await expect(page.locator('#keyboard-voice-inline-error')).toHaveText('Send failed. Try again.')
      }
    })
  }
}
