import { expect, test } from './browser-test'

test('manual browsing survives repeated composition and IME-name refreshes, but selection changes reveal the candidate', async ({
  page,
}) => {
  await page.goto('/')
  const view = {
    preedit: '',
    composing: false,
    candidates: Array.from({ length: 20 }, (_, index) => `候选词语${index + 1}`),
    selected: 0,
    selectionStart: 0,
    selectionEnd: 0,
  }
  await page.evaluate((view) => window.__xsoKeyboardIme!.showComposition(view), view)
  const viewport = page.locator('.ime-candidates')
  await viewport.hover()
  await page.mouse.wheel(600, 0)
  await expect
    .poll(() => viewport.evaluate((element) => element.scrollLeft))
    .toBeGreaterThan(300)
  const browsed = await viewport.evaluate((element) => element.scrollLeft)
  await page.evaluate((view) => {
    window.__xsoKeyboardIme!.showComposition(view)
    window.__xsoKeyboardIme!.showComposerState({
      active: true,
      ime: 'Another IME',
      profiles: [],
      activeProfile: -1,
    })
  }, view)
  await page.waitForTimeout(50)
  expect(await viewport.evaluate((element) => element.scrollLeft)).toBe(browsed)
  await page.evaluate(
    (view) => window.__xsoKeyboardIme!.showComposition({ ...view, selected: 19 }),
    view,
  )
  await expect
    .poll(() => viewport.evaluate((element) => element.scrollLeft))
    .toBeGreaterThan(browsed)
  const selected = await page.locator('.ime-selected').boundingBox()
  const visible = await viewport.boundingBox()
  expect(selected!.x + selected!.width).toBeLessThanOrEqual(
    visible!.x + visible!.width + 1,
  )
})
