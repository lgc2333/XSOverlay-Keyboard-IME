import { resolve } from 'node:path'
import process from 'node:process'
import { expect, test } from './browser-test'

const bundle = resolve(process.env.IME_TEST_BUNDLE || 'dist/keyboard-ime.js')
const composing = {
  preedit: 'ni', candidates: ['你'], selected: 0, composing: true,
  selectionStart: 2, selectionEnd: 2,
}
const idle = {
  preedit: '', candidates: [], selected: -1, composing: false,
  selectionStart: 0, selectionEnd: 0,
}

test.beforeEach(async ({ page }) => {
  await page.setContent(`
    <style>
      :root { --theme-dark: #20252c; --theme-mid: #343b44; --theme-hi: #485260;
        --theme-contrasting: #eef2f5; --theme-accent: #5cfbe9;
        --keyboard-control-size: 44px; --keyboard-toolbar-inset: 8px; }
      #keyboard-container { width: 700px; }
      #keyboard-voice-input { width: 400px; }
    </style>
    <div id="keyboard-container">
      <input id="keyboard-voice-input" value="你好">
      <button id="keyboard-voice-send">发送</button>
      <button id="clear">清空</button>
      <button id="elsewhere">其他控件</button>
      <div id="keyboard-background"></div>
    </div>
  `)
  await page.evaluate(() => {
    window.hostMessages = []
    window.vuplex = { postMessage: message => window.hostMessages.push(message) }
    document.getElementById('clear').addEventListener('pointerdown', (event) => {
      event.preventDefault()
      document.getElementById('keyboard-voice-input').value = ''
      window.dispatchEvent(new Event('blur'))
    })
    const composer = document.getElementById('keyboard-voice-input')
    composer.focus()
    composer.setSelectionRange(2, 2)
  })
  await page.addScriptTag({ path: bundle })
})

test('未知输入法降低亮度，保留下拉选择且不移动输入框焦点', async ({ page }, testInfo) => {
  await page.evaluate(() => window.__xsoKeyboardIme.showComposerState({
    active: true, ime: '小狼毫',
    profiles: [{ name: '小狼毫', language: '中文' }, { name: '美式键盘', language: '英语' }],
    activeProfile: 0,
  }))
  const chip = page.locator('.ime-chip')
  const knownColor = await chip.evaluate(element => getComputedStyle(element).color)
  await page.evaluate(() => window.__xsoKeyboardIme.showComposerState({
    active: true, ime: '',
    profiles: [{ name: '小狼毫', language: '中文' }, { name: '美式键盘', language: '英语' }],
    activeProfile: -1,
  }))
  await expect(chip).toHaveText('未知输入法')
  expect(await chip.evaluate(element => getComputedStyle(element).color)).not.toBe(knownColor)
  await chip.click()
  await expect(page.getByRole('menu')).toBeVisible()
  await expect(page.locator('.ime-profile-check')).toHaveCount(0)
  await page.screenshot({ path: testInfo.outputPath('unknown-ime-menu.png') })
  await page.getByRole('menuitem').filter({ hasText: '美式键盘' }).click()
  expect(await page.evaluate(() => window.hostMessages)).toContain('xso-keyboard-ime profile 1')
  await expect(page.locator('#keyboard-voice-input')).toBeFocused()
})
