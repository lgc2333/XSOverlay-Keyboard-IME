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
    content:
      "const input = document.getElementById('keyboard-voice-input'); const send = document.getElementById('keyboard-voice-send'); const submit = () => send.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, button: 0, buttons: 1, pointerId: 1, pointerType: 'mouse' })); send.addEventListener('pointerdown', event => { event.preventDefault(); input.blur(); window.messages.push('native-submit') }); input.addEventListener('keydown', event => { if (event.key === 'Enter') submit() }); document.getElementById('key-RETURN').addEventListener('pointerdown', submit)",
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

test('defers the virtual Return send through the same handshake', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('draft')
  await page.locator('#key-RETURN').dispatchEvent(
    'pointerdown',
    { button: 0, buttons: 1, pointerId: 7, pointerType: 'mouse' },
  )
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
