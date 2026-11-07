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
    window.__xsoKeyboardIme!.showSwitchIcon({ width: 1, height: 1, rgba: '/wAA/w==' }),
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
