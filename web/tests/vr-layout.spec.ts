import { expect, test } from './browser-test'

test('the switch shares space without moving either Ctrl, including animation and rebuild', async ({
  page,
}) => {
  await page.goto('/')
  const original = await page.locator('#bottom').evaluate((row) => row.outerHTML)
  await page.locator('#keyboard-voice-input').focus()
  const baseline = await page.locator('#bottom').evaluate((row) => ({
    width: row.getBoundingClientRect().width,
    positions: [...row.querySelectorAll('[id$="CONTROL"]')].map(
      (key) => key.getBoundingClientRect().x,
    ),
  }))
  await page.evaluate(() =>
    window.__xsoKeyboardIme!.showSwitchIcon({ kind: 'image', enabled: true, tooltip: '', tint: false, width: 1, height: 1, rgba: '/wAA/w==' }),
  )
  const key = page.locator('.ime-switch-key')
  await expect(key).toBeVisible()
  const geometry = await page.locator('#bottom').evaluate(async (row) => {
    const samples = []
    for (let index = 0; index < 25; index++) {
      await new Promise(requestAnimationFrame)
      samples.push({
        width: row.getBoundingClientRect().width,
        positions: [...row.querySelectorAll('[id$="CONTROL"]')].map(
          (key) => key.getBoundingClientRect().x,
        ),
      })
    }
    const button = row.querySelector('.ime-switch-key')!
    const alt = row.querySelector('#key-LALT')!
    return {
      samples,
      previous: button.previousElementSibling?.id,
      width: button.getBoundingClientRect().width,
      gap: button.getBoundingClientRect().left - alt.getBoundingClientRect().right,
    }
  })
  expect(geometry.previous).toBe('key-LALT')
  expect(geometry.width).toBe(64)
  expect(geometry.gap).toBe(4)
  for (const sample of geometry.samples) expect(sample).toEqual(baseline)
  await key.click()
  expect(await page.evaluate(() => window.messages)).toContain(
    'xso-keyboard-ime switch',
  )
  await page.evaluate((original) => {
    const template = document.createElement('template')
    template.innerHTML = original
    document.getElementById('bottom')!.replaceWith(template.content)
  }, original)
  await expect(key).toHaveCount(1)
  expect(
    await page
      .locator('#bottom')
      .evaluate((row) => ({
        width: row.getBoundingClientRect().width,
        positions: [...row.querySelectorAll('[id$="CONTROL"]')].map(
          (key) => key.getBoundingClientRect().x,
        ),
      })),
  ).toEqual(baseline)
  await page.evaluate(() => window.__xsoKeyboardIme!.showSwitchIcon(null))
  await expect(page.locator('#key-SPACE')).toHaveCSS('width', '320px')
  await expect(key).toHaveCount(0)
})

test('a mode glyph uses its system font and keeps the same key geometry', async ({ page }) => {
  await page.goto('/')
  await page.locator('#keyboard-voice-input').focus()
  await page.evaluate(() => window.__xsoKeyboardIme!.showSwitchIcon({
    kind: 'glyph', enabled: true, tooltip: '中文模式', glyph: '\uE982', font: 'Segoe Fluent Icons',
  }))
  const key = page.locator('.ime-switch-key')
  await expect(key).toBeVisible()
  await expect(key.locator('.ime-mode-glyph')).toHaveText('\uE982')
  await expect(key.locator('.ime-mode-glyph')).toHaveCSS('font-family', '"Segoe Fluent Icons"')
  await expect(key).toHaveAttribute('title', '中文模式')
  await expect(page.locator('#key-SPACE')).toHaveCSS('width', '252px')
})

test('a disabled placeholder stays visible and every press is inert', async ({ page }) => {
  const sounds: string[] = []
  page.on('request', request => { if (request.url().includes('/keyboard-sounds/')) sounds.push(request.url()) })
  await page.goto('/')
  await page.locator('#keyboard-voice-input').focus()
  await page.evaluate(() => {
    Object.defineProperty(performance, 'getEntriesByType', { value: () => [{ name: `${location.origin}/keyboard-sounds/test/tap.ogg` }] })
    window.__xsoKeyboardIme!.showSwitchIcon({ kind: 'placeholder', enabled: false, tooltip: '' })
    window.messages.length = 0
  })
  const key = page.locator('.ime-switch-key')
  await expect(key).toBeVisible()
  await expect(key).toHaveAttribute('aria-disabled', 'true')
  await expect(key).toHaveText('中/英')
  const staticColor = await key.evaluate(key => getComputedStyle(key).backgroundColor)
  await key.evaluate(key => key.classList.add('pointer-hover'))
  await key.hover()
  await expect(key).toHaveCSS('box-shadow', 'none')
  await expect(key).toHaveCSS('scale', '1')
  await key.click()
  expect(await key.evaluate(key => key.getAnimations().length)).toBe(0)
  await expect(key).toHaveCSS('background-color', staticColor)
  expect(await page.evaluate(() => window.messages)).toEqual([])
  expect(sounds).toEqual([])
  await expect(page.locator('#keyboard-voice-input')).toBeFocused()
})

test('a monochrome mode image follows theme color without changing its mask', async ({ page }) => {
  await page.goto('/')
  await page.locator('#keyboard-voice-input').focus()
  await page.evaluate(() => window.__xsoKeyboardIme!.showSwitchIcon({
    kind: 'image', enabled: true, tooltip: '', tint: true, width: 1, height: 1, rgba: '/////w==',
  }))
  const image = page.locator('.ime-mode-mask')
  await expect(image).toBeVisible()
  const mask = await image.evaluate(image => getComputedStyle(image).maskImage)
  expect(mask).toContain('data:image/png;')
  await page.locator('.ime-switch-key').evaluate(key => key.style.color = 'rgb(255, 0, 0)')
  await expect(image).toHaveCSS('background-color', 'rgb(255, 0, 0)')
  await expect(image).toHaveCSS('mask-image', mask)
})
