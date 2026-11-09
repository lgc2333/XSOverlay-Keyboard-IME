import type { Page } from '@playwright/test'
import { expect, test } from './browser-test'

async function snapshots(page: Page): Promise<Array<{ active: boolean; text: string; submitting: boolean }>> {
  return page.evaluate(() => window.messages
    .filter(message => message.startsWith('xso-keyboard-ime chatbox '))
    .map(message => JSON.parse(message.slice('xso-keyboard-ime chatbox '.length))))
}

test.beforeEach(async ({ page }) => {
  await page.goto('/')
  await page.evaluate(() => {
    const toggle = document.createElement('button')
    toggle.id = 'keyboard-toolbar-button-vrc'
    toggle.setAttribute('aria-pressed', 'true')
    document.getElementById('keyboard-container')!.append(toggle)
  })
  await page.locator('#keyboard-voice-input').focus()
})

test('reports the focused VRC composer and stops on blur without clearing text', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('hello VRChat')
  await expect.poll(async () => (await snapshots(page)).at(-1)).toEqual({
    revision: 0, active: true, text: 'hello VRChat', submitting: false,
  })
  await page.locator('#key-F1').focus()
  await expect.poll(async () => (await snapshots(page)).at(-1)?.active).toBe(false)
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('hello VRChat')
})

test('uses the existing inline error unchanged while preserving the full local text', async ({ page }) => {
  await page.evaluate(() => {
    const wrap = document.createElement('div')
    wrap.id = 'keyboard-composer-wrap'
    const input = document.getElementById('keyboard-voice-input')!
    input.before(wrap)
    wrap.append(input)
    const error = document.createElement('span')
    error.id = 'keyboard-voice-inline-error'
    error.hidden = true
    wrap.append(error)
    input.focus()
  })
  const text = 'a'.repeat(145)
  await page.locator('#keyboard-voice-input').fill(text)
  const error = page.locator('#keyboard-voice-inline-error')
  await expect(error).toHaveText('VRChat chatbox text must be 144 characters or fewer.')
  await expect(error).toBeVisible()
  await expect(page.locator('#keyboard-composer-wrap')).toHaveClass(/voice-error/)
  await expect(page.locator('#keyboard-voice-input')).toHaveValue(text)
  await page.locator('#keyboard-voice-input').fill('a'.repeat(144))
  await expect(error).toBeHidden()
})

test('excludes inline codes and retains the committed selection until IME writeback', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('before SELECT after')
  await expect.poll(async () => (await snapshots(page)).at(-1)?.text).toBe('before SELECT after')
  await page.evaluate(() => {
    const input = document.getElementById('keyboard-voice-input') as HTMLInputElement
    input.setSelectionRange(7, 13)
    window.__xsoKeyboardIme!.showComposition({
      preedit: 'ni', composing: true, candidates: ['你'], selected: 0, selectionStart: 2, selectionEnd: 2,
    })
  })
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('before SELECT after')
  expect((await snapshots(page)).at(-1)?.text).toBe('before SELECT after')
  await page.evaluate(() => window.__xsoKeyboardIme!.insertText('你'))
  await expect.poll(async () => (await snapshots(page)).at(-1)?.text).toBe('before 你 after')
  expect((await snapshots(page)).some(state => state.text.includes('ni'))).toBe(false)
})

test('observes speech and native clears that update the toolbar without input events', async ({ page }) => {
  await page.evaluate(() => {
    const input = document.getElementById('keyboard-voice-input') as HTMLInputElement
    input.value = 'provisional speech'
    input.setAttribute('aria-busy', 'false')
  })
  await expect.poll(async () => (await snapshots(page)).at(-1)?.text).toBe('provisional speech')
  await page.evaluate(() => {
    const input = document.getElementById('keyboard-voice-input') as HTMLInputElement
    input.value = ''
    input.placeholder = 'Click to type, or speak to yap!'
  })
  await expect.poll(async () => (await snapshots(page)).at(-1)?.text).toBe('')
})

test('VRC toggle and native send pending state reach the host without changing native sending', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('draft')
  await page.evaluate(() => document.getElementById('keyboard-voice-input')!.setAttribute('aria-busy', 'true'))
  await expect.poll(async () => (await snapshots(page)).at(-1)?.submitting).toBe(true)
  await page.evaluate(() => document.getElementById('keyboard-toolbar-button-vrc')!.setAttribute('aria-pressed', 'false'))
  await expect.poll(async () => (await snapshots(page)).at(-1)?.active).toBe(false)
  expect(await page.evaluate(() => window.messages)).not.toContain('native-submit')
})
