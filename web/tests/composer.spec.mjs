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

test('清空后旧组字回放不能恢复内容，重新聚焦可开始新组字', async ({ page }) => {
  await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), composing)
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('你好ni')
  await page.locator('#clear').click()
  // Reproduce the inactive state refresh that used to replay the cached composition.
  await page.evaluate((view) => {
    window.__xsoKeyboardIme.showComposerState({ active: false, ime: '', profiles: [], activeProfile: -1 })
    window.__xsoKeyboardIme.showComposition(view)
  }, composing)
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('')
  await expect.poll(() => page.evaluate(() => window.hostMessages)).toContain('xso-keyboard-ime cancel')
  expect(await page.evaluate(() => window.__xsoKeyboardIme.insertText('旧提交'))).toBe('focus-lost')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('')
  await page.evaluate((view) => {
    window.__xsoKeyboardIme.showComposition(view)
    window.dispatchEvent(new Event('focus'))
    document.getElementById('keyboard-voice-input').focus()
  }, idle)
  await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), composing)
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('ni')
})

test('清空早于组字结束回调时不会恢复基础文本', async ({ page }) => {
  await page.evaluate((views) => {
    window.__xsoKeyboardIme.showComposition(views.composing)
    document.getElementById('keyboard-voice-input').value = ''
    window.__xsoKeyboardIme.showComposition(views.idle)
  }, { composing, idle })
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('')
})

for (const baseValue of ['', '你好']) {
  test(`清空${baseValue === '' ? '空框' : '已有文本'}后，焦点释放前的连续组字回调不恢复旧编码`, async ({ page }) => {
    await page.evaluate(({ baseValue, view }) => {
      const composer = document.getElementById('keyboard-voice-input')
      composer.value = baseValue
      composer.setSelectionRange(baseValue.length, baseValue.length)
      window.__xsoKeyboardIme.showComposition(view)
      composer.value = ''
      // The original page requests focus release asynchronously; the DOM still has focus.
      window.__xsoKeyboardIme.showComposition(view)
      window.__xsoKeyboardIme.showComposition(view)
    }, { baseValue, view: composing })
    await expect(page.locator('#keyboard-voice-input')).toBeFocused()
    await expect(page.locator('#keyboard-voice-input')).toHaveValue('')
    await expect(page.locator('.ime-candidate')).toHaveCount(0)
    expect(await page.evaluate(() => window.hostMessages.filter(message => message === 'xso-keyboard-ime cancel'))).toHaveLength(1)

    // Genuine late commits remain deliverable while the cancelled view is discarded.
    expect(await page.evaluate(() => window.__xsoKeyboardIme.insertText('迟到提交'))).toBe('inserted')
    await page.evaluate(({ idle, composing }) => {
      window.__xsoKeyboardIme.showComposition(idle)
      window.__xsoKeyboardIme.showComposition(composing)
    }, { idle, composing })
    await expect(page.locator('#keyboard-voice-input')).toHaveValue('迟到提交ni')
    await expect(page.locator('.ime-candidate')).toHaveText(['1你'])
  })
}

test('输入框仍聚焦时，布局重建保留组字并正确写回提交', async ({ page }) => {
  await page.evaluate((view) => {
    window.__xsoKeyboardIme.showComposition(view)
    const composer = document.getElementById('keyboard-voice-input')
    const replacement = composer.cloneNode()
    replacement.value = '你好'
    composer.replaceWith(replacement)
  }, composing)
  const composer = page.locator('#keyboard-voice-input')
  await expect(composer).toBeFocused()
  await expect(composer).toHaveValue('你好ni')
  await page.evaluate(() => window.__xsoKeyboardIme.insertText('你'))
  await expect(composer).toHaveValue('你好你')
  expect(await page.evaluate(() => window.hostMessages)).not.toContain('xso-keyboard-ime cancel')
})

test('真正失焦后，布局重建不能抢回其他控件的焦点', async ({ page }) => {
  await page.locator('#elsewhere').click()
  await page.evaluate(() => {
    const composer = document.getElementById('keyboard-voice-input')
    composer.replaceWith(composer.cloneNode())
  })
  await expect(page.locator('#elsewhere')).toBeFocused()
})

test('组字中整页失活后，重建不抢焦并取消旧组字', async ({ page }) => {
  await page.evaluate((view) => {
    window.__xsoKeyboardIme.showComposition(view)
    window.dispatchEvent(new Event('blur'))
    const composer = document.getElementById('keyboard-voice-input')
    const replacement = composer.cloneNode()
    replacement.setSelectionRange(composer.selectionStart, composer.selectionEnd)
    composer.replaceWith(replacement)
  }, composing)
  await expect(page.locator('#keyboard-voice-input')).not.toBeFocused()
  await expect.poll(() => page.evaluate(() => window.hostMessages)).toContain('xso-keyboard-ime cancel')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('你好')
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
