import { expect, test } from './browser-test'

for (const input of ['DOM', 'pointer', 'touch']) {
  for (const composing of [false, true]) {
    test(`${input} Return ${composing ? 'belongs to the IME' : 'sends once through the page'}`, async ({ page, request }) => {
      if (input !== 'DOM') {
        const available = await request.get('/native-keyboard-available')
        test.skip(!await available.json(), 'Requires the installed XSOverlay keyboard modules (XSOVERLAY_GAME_DIR).')
      }
      await page.goto('/')
      await page.locator('#keyboard-voice-input').fill('draft')
      if (composing) {
        await page.evaluate(() => window.__xsoKeyboardIme!.showComposition({
          preedit: 'ni', composing: true, candidates: [], selected: -1, selectionStart: 2, selectionEnd: 2,
        }))
      }
      if (input === 'DOM') {
        await page.keyboard.press('Enter')
      } else {
        await page.locator('#key-RETURN').evaluate((key, input) => {
          if (input === 'pointer') {
            key.dispatchEvent(new PointerEvent('pointerdown', { bubbles: true, cancelable: true, pointerId: 1, pointerType: 'mouse' }))
            key.dispatchEvent(new PointerEvent('pointerup', { bubbles: true, pointerId: 1, pointerType: 'mouse' }))
          } else {
            const touch = new Touch({ identifier: 1, target: key })
            key.dispatchEvent(new TouchEvent('touchstart', { bubbles: true, cancelable: true, changedTouches: [touch], touches: [touch], targetTouches: [touch] }))
            key.dispatchEvent(new TouchEvent('touchend', { bubbles: true, changedTouches: [touch] }))
          }
        }, input)
      }
      const requests = () => page.evaluate(() => window.messages.filter(message => message.startsWith('xso-keyboard-ime return ')))
      await expect.poll(requests).toHaveLength(1)
      expect(await page.evaluate(() => window.messages)).not.toContain('native-submit')
      await expect(page.locator('#keyboard-voice-input')).toBeFocused()
      const id = Number((await requests())[0]!.split(' ').at(-1))
      await page.evaluate(({ id, composing }) => {
        window.__xsoKeyboardIme!.resolveReturn(id, composing)
        window.__xsoKeyboardIme!.resolveReturn(id, composing)
      }, { id, composing })
      expect((await page.evaluate(() => window.messages)).filter(message => message === 'native-submit')).toHaveLength(composing ? 0 : 1)
      expect(await requests()).toHaveLength(1)
      if (composing) {
        await expect(page.locator('#keyboard-voice-input')).toBeFocused()
        await expect(page.locator('#keyboard-voice-input')).toHaveValue('draftni')
      }
    })
  }
}

test('native Return modules use the installed HTTP paths and reject stale paths', async ({ request }) => {
  const available = await request.get('/native-keyboard-available')
  const installed = await available.json()
  for (const name of ['keyboard-input.js', 'keyboard-audio.js']) {
    const response = await request.get(`/apps/UserInterface/Default/Keyboard/js/${name}`)
    if (installed) expect(response.status()).toBe(200)
    else expect([200, 404]).toContain(response.status())
    if (response.ok()) expect(response.headers()['content-type']).toContain('text/javascript')
    expect((await request.get(`/UserInterface/Default/Keyboard/js/${name}`)).status()).toBe(404)
  }
  expect((await request.get('/apps/UserInterface/Default/Keyboard/js/unknown.js')).status()).toBe(404)
})

test('Return sound loads the installed audio module without an audio device', async ({ page, request }) => {
  const available = await request.get('/native-keyboard-available')
  test.skip(!await available.json(), 'Requires the installed XSOverlay keyboard modules (XSOVERLAY_GAME_DIR).')
  await page.addInitScript(() => {
    Object.defineProperty(window, 'AudioContext', { value: undefined })
    Object.defineProperty(window, 'webkitAudioContext', { value: undefined })
  })
  await page.goto('/')
  const loaded = page.waitForResponse(response => response.url().endsWith('/apps/UserInterface/Default/Keyboard/js/keyboard-audio.js'))
  await page.evaluate(() => window.__xsoKeyboardIme!.playReturnSound({ soundPack: 'test', audio: 'enter', enabled: true }))
  expect((await loaded).ok()).toBe(true)
})

test('Return waits for the host decision and preserves repeated DOM Enter events', async ({
  page,
}) => {
  await page.goto('/')
  await page.locator('#keyboard-voice-input').fill('nihc')
  await page.keyboard.down('Enter')
  await expect(page.locator('#keyboard-voice-input')).toBeFocused()
  const request = await page.evaluate(() =>
    window.messages.find((message) => message.startsWith('xso-keyboard-ime return ')),
  )
  expect(request).toBeDefined()
  await page.evaluate(
    (request) =>
      window.__xsoKeyboardIme!.resolveReturn(Number(request!.split(' ').at(-1)), true),
    request,
  )
  await page.keyboard.down('Enter')
  expect(
    await page.evaluate(
      () =>
        window.messages.filter((message) =>
          message.startsWith('xso-keyboard-ime return '),
        ).length,
    ),
  ).toBe(2)
  await page.keyboard.up('Enter')
  await page.evaluate(() => window.__xsoKeyboardIme!.resolveReturn(2, false))
  await expect(page.locator('#keyboard-voice-input')).not.toBeFocused()
  expect(
    await page.evaluate(
      () => window.messages.filter((message) => message === 'native-submit').length,
    ),
  ).toBe(1)
})

test('losing page focus cancels Return while its native controller is loading', async ({
  page,
  request,
}) => {
  const available = await request.get('/native-keyboard-available')
  test.skip(!await available.json(), 'Requires the installed XSOverlay keyboard modules (XSOVERLAY_GAME_DIR).')
  const moduleUrl = '/apps/UserInterface/Default/Keyboard/js/keyboard-input.js'
  let unblock = (): void => {}
  const blocked = new Promise<void>(resolve => { unblock = resolve })
  let loading = (): void => {}
  const started = new Promise<void>(resolve => { loading = resolve })
  await page.route(`**${moduleUrl}`, async route => {
    const response = await route.fetch()
    loading()
    await blocked
    await route.fulfill({ response })
  })
  await page.goto('/')
  await page.locator('#keyboard-voice-input').focus()
  await page.locator('#key-RETURN').click()
  await started
  await page.evaluate(() => window.dispatchEvent(new Event('blur')))
  unblock()
  await page.evaluate(async moduleUrl => {
    await import(moduleUrl)
    await new Promise(requestAnimationFrame)
  }, moduleUrl)
  expect(await page.evaluate(() => window.messages.some(message => message.startsWith('xso-keyboard-ime return ')))).toBe(false)
  expect(await page.evaluate(() => window.messages)).not.toContain('native-submit')
})

test('held VR Return keeps producing host requests and stops on release', async ({
  page,
  request,
}) => {
  const available = await request.get('/native-keyboard-available')
  test.skip(!await available.json(), 'Requires the installed XSOverlay keyboard modules (XSOVERLAY_GAME_DIR).')
  await page.goto('/')
  await page.locator('#keyboard-voice-input').focus()
  await page.evaluate(() => {
    window.vuplex!.postMessage = (message) => {
      window.messages.push(message)
      if (message.startsWith('xso-keyboard-ime return ')) {
        queueMicrotask(() =>
          window.__xsoKeyboardIme!.resolveReturn(
            Number(message.split(' ').at(-1)),
            true,
          ),
        )
      }
    }
  })
  await page.locator('#key-RETURN').hover()
  await page.mouse.down()
  await expect
    .poll(() =>
      page.evaluate(
        () =>
          window.messages.filter((message) =>
            message.startsWith('xso-keyboard-ime return '),
          ).length,
      ),
    )
    .toBeGreaterThanOrEqual(3)
  await page.mouse.up()
  const released = await page.evaluate(
    () =>
      window.messages.filter((message) =>
        message.startsWith('xso-keyboard-ime return '),
      ).length,
  )
  await page.waitForTimeout(150)
  expect(
    await page.evaluate(
      () =>
        window.messages.filter((message) =>
          message.startsWith('xso-keyboard-ime return '),
        ).length,
    ),
  ).toBe(released)
  await expect(page.locator('#keyboard-voice-input')).toBeFocused()
  expect(await page.evaluate(() => window.messages)).not.toContain('native-submit')
  expect(
    await page.evaluate(
      () =>
        window.messages.filter((message) =>
          message.startsWith('xso-keyboard-ime return-feedback '),
        ).length,
    ),
  ).toBe(1)
})
