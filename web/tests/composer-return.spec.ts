import { expect, test } from './browser-test'

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
  const moduleUrl = '/UserInterface/Default/Keyboard/js/keyboard-input.js'
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
