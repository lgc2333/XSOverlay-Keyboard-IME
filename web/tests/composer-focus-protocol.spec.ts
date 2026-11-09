import { expect, test } from './browser-test'

test.beforeEach(async ({ page }) => {
  await page.setContent(
    '<input id="before"><input id="keyboard-voice-input"><button id="keyboard-voice-send">Send</button><button id="after">After</button><div id="keyboard-background"></div>',
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
})

test('script focus does not grant; clicking an already focused composer requests once without moving its caret', async ({
  page,
}) => {
  const composer = page.locator('#keyboard-voice-input')
  await composer.fill('abcdef')
  await composer.focus()
  expect(await requests(page)).toHaveLength(0)
  await composer.click({ position: { x: 20, y: 10 } })
  expect(await requests(page)).toHaveLength(1)
  await page.evaluate(() => {
    const api = window.__xsoKeyboardIme!
    api.applyFocus({ kind: 'held', document: 'document-a', revision: 1, request: 1 })
    // Duplicate clear/open from the earlier handshake cannot blur/reset a held request.
    api.applyFocus({ kind: 'clear', document: 'document-a', revision: 1, request: 0 })
    api.applyFocus({ kind: 'open', document: 'document-a', revision: 1, request: 0 })
  })
  await expect(composer).toBeFocused()
  await composer.click()
  expect(await requests(page)).toHaveLength(1)
})

test('revoke clears focus before ack, rejects input while closed and accepts one fresh post-open action', async ({
  page,
}) => {
  await page.locator('#keyboard-voice-input').click()
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'clear',
      document: 'document-a',
      revision: 2,
      request: 0,
    }),
  )
  await expect(page.locator('#keyboard-voice-input')).not.toBeFocused()
  await page.locator('#keyboard-voice-input').click()
  expect(await requests(page)).toHaveLength(1)
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'open',
      document: 'document-a',
      revision: 2,
      request: 0,
    }),
  )
  // It is already focused from the rejected action; a real fresh click must still work.
  await page.locator('#keyboard-voice-input').click()
  expect((await requests(page)).map((item) => item.revision)).toEqual([1, 2])
})

test('Tab up uses the actual navigation target; a cross-revision pair never grants', async ({
  page,
}) => {
  await page.locator('#before').focus()
  await page.keyboard.press('Tab')
  expect(await requests(page)).toHaveLength(1)
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'clear',
      document: 'document-a',
      revision: 2,
      request: 0,
    }),
  )
  await page.locator('#before').focus()
  await page.keyboard.down('Tab')
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'open',
      document: 'document-a',
      revision: 2,
      request: 0,
    }),
  )
  await page.keyboard.up('Tab')
  expect(await requests(page)).toHaveLength(1)
})

test('late binding/old document messages cannot reset a new binding', async ({
  page,
}) => {
  await page.evaluate(() => {
    const api = window.__xsoKeyboardIme!
    api.bindFocus('document-b', 2, api.focusDocument)
    api.applyFocus({ kind: 'clear', document: 'document-b', revision: 1, request: 0 })
    api.applyFocus({ kind: 'open', document: 'document-b', revision: 1, request: 0 })
    api.bindFocus('document-a', 1, api.focusDocument)
    api.applyFocus({ kind: 'clear', document: 'document-a', revision: 9, request: 0 })
  })
  await page.locator('#keyboard-voice-input').click()
  expect((await requests(page)).map((item) => item.document)).toEqual(['document-b'])
})

test('revocation retires pending Return before reopening the same composer', async ({
  page,
}) => {
  const composer = page.locator('#keyboard-voice-input')
  await composer.click()
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.applyFocus({
      kind: 'held',
      document: 'document-a',
      revision: 1,
      request: 1,
    }),
  )
  await page.keyboard.press('Enter')
  const id = await page.evaluate(() => {
    const message = window.messages.find((item) => item.includes('"kind":"return"'))!
    return (
      JSON.parse(message.slice('xso-keyboard-ime focus '.length)) as { index: number }
    ).index
  })
  await page.evaluate(() => {
    const api = window.__xsoKeyboardIme!
    api.applyFocus({ kind: 'clear', document: 'document-a', revision: 2, request: 0 })
    api.applyFocus({ kind: 'open', document: 'document-a', revision: 2, request: 0 })
    document.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') window.messages.push('stale-return-replayed')
    })
  })
  await composer.click()
  await page.evaluate((id) => window.__xsoKeyboardIme!.resolveReturn(id, false), id)
  expect(
    await page.evaluate(() => window.messages.includes('stale-return-replayed')),
  ).toBe(false)
})

async function requests(
  page: import('@playwright/test').Page,
): Promise<{ document: string; revision: number }[]> {
  return page.evaluate(() =>
    window.messages
      .filter((item) => item.startsWith('xso-keyboard-ime focus '))
      .map((item) => JSON.parse(item.slice('xso-keyboard-ime focus '.length)))
      .filter((item) => item.kind === 'request'),
  )
}
