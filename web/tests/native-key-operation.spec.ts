import { expect, test } from './browser-test'

test.beforeEach(async ({ page }) => {
  await page.setContent('<input id="keyboard-voice-input">')
  await page.evaluate(() => {
    window.messages = []
    window.vuplex = { postMessage: message => window.messages.push(message) }
  })
  await page.addScriptTag({ path: 'dist/keyboard-ime.js' })
})

test('native edit ack follows default action, including a no-op edge', async ({ page }) => {
  await page.locator('input').fill('ab')
  await page.evaluate(() => window.__xsoKeyboardIme!.armNativeKey('1', 'Backspace', false))
  await page.keyboard.down('Backspace')
  await expect.poll(() => page.evaluate(() => window.messages)).toContain('xso-keyboard-ime native 1 ok')
  await expect(page.locator('input')).toHaveValue('a')
  await page.evaluate(() => window.__xsoKeyboardIme!.armNativeKey('2', 'Backspace', true))
  await page.keyboard.up('Backspace')
  await expect.poll(() => page.evaluate(() => window.messages)).toContain('xso-keyboard-ime native 2 ok')
  await page.locator('input').fill('')
  await page.evaluate(() => window.__xsoKeyboardIme!.armNativeKey('3', 'ArrowLeft', false))
  await page.keyboard.down('ArrowLeft')
  await expect.poll(() => page.evaluate(() => window.messages)).toContain('xso-keyboard-ime native 3 ok')
  await page.keyboard.up('ArrowLeft')
})

test('a submission without a trusted native event fails once and releases pending state', async ({ page }) => {
  await page.evaluate(() => {
    const api = window.__xsoKeyboardIme!
    api.armNativeKey('1', 'Backspace', false)
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Backspace' }))
    api.finishNativeKey('1')
    api.finishNativeKey('1')
  })
  await expect.poll(() => page.evaluate(() => window.messages.filter(value => value.includes('native 1')))).toEqual(['xso-keyboard-ime native 1 failed'])
  expect(await page.evaluate(() => window.__xsoKeyboardIme!.armNativeKey('2', 'ArrowLeft', false))).toBe('armed')
  await page.evaluate(() => window.__xsoKeyboardIme!.cancelNativeKey())
  await expect.poll(() => page.evaluate(() => window.messages)).toContain('xso-keyboard-ime native 2 failed')
})

test('shifted punctuation and native select-all, deletion and undo keep their defaults', async ({ page }) => {
  let sequence = 10
  const edge = async (key: string, up = false, browserKey = key): Promise<void> => {
    const token = String(++sequence)
    await page.evaluate(({ token, key, up }) => window.__xsoKeyboardIme!.armNativeKey(token, key, up), { token, key, up })
    if (up) await page.keyboard.up(browserKey)
    else await page.keyboard.down(browserKey)
    await expect.poll(() => page.evaluate(() => window.messages)).toContain(`xso-keyboard-ime native ${token} ok`)
  }
  await page.locator('input').focus()
  await edge('Shift')
  await edge('1', false, '!')
  await edge('1', true, '!')
  await edge('Shift', true)
  await expect(page.locator('input')).toHaveValue('!')
  await page.locator('input').fill('native document')
  await edge('Control')
  await edge('a')
  await edge('a', true)
  await edge('Control', true)
  expect(await page.locator('input').evaluate((element: HTMLInputElement) => [element.selectionStart, element.selectionEnd])).toEqual([0, 15])
  await edge('Backspace')
  await edge('Backspace', true)
  await expect(page.locator('input')).toHaveValue('')
  await edge('Control')
  await edge('z')
  await edge('z', true)
  await edge('Control', true)
  await expect(page.locator('input')).toHaveValue('native document')
})

test('view blur cancels an armed native edge and does not poison the next operation', async ({ page }) => {
  await page.evaluate(() => {
    window.__xsoKeyboardIme!.armNativeKey('old', 'Backspace', false)
    window.dispatchEvent(new Event('blur'))
  })
  await expect.poll(() => page.evaluate(() => window.messages)).toContain('xso-keyboard-ime native old failed')
  expect(await page.evaluate(() => window.__xsoKeyboardIme!.armNativeKey('new', 'ArrowLeft', false))).toBe('armed')
  await page.evaluate(() => window.__xsoKeyboardIme!.cancelNativeKey())
})
