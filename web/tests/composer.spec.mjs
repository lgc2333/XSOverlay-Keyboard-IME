import { expect, test } from './browser-test'

const composing = {
  preedit: 'ni', candidates: ['你'], selected: 0, composing: true,
  selectionStart: 2, selectionEnd: 2,
}
const idle = {
  preedit: '', candidates: [], selected: -1, composing: false,
  selectionStart: 0, selectionEnd: 0,
}

test.beforeEach(async ({ page, request }) => {
  test.skip(!await (await request.get('/native-keyboard-available')).json(), 'Requires installed XSOverlay modules.')
  await page.goto('/')
  await page.evaluate(async () => {
    const prefix = '/apps/UserInterface/Default/Keyboard/js/'
    const { createKeyboardState } = await import(`${prefix}keyboard-state.js`)
    const { createKeyboardSpeechView } = await import(`${prefix}keyboard-speech-view.js`)
    document.getElementById('keyboard-voice-input').remove()
    document.getElementById('keyboard-voice-send').remove()
    const row = document.createElement('div')
    row.id = 'keyboard-toolbar-row'
    document.getElementById('keyboard-container').prepend(row)
    const style = document.createElement('style')
    style.textContent = `#keyboard-container {width:700px} #keyboard-toolbar-row {display:flex;gap:10px;height:64px} .keyboard-toolbar-bubble {position:relative;display:flex;align-items:center;box-sizing:border-box;height:64px;padding:8px;background:var(--theme-dark)} #keyboard-composer-wrap {flex:1;min-width:0;gap:8px} #keyboard-voice-input {min-width:0;flex:1}`
    document.head.append(style)
    const view = createKeyboardSpeechView({
      state: createKeyboardState(),
      createControl: id => Object.assign(document.createElement('button'), { id, textContent: id }),
      bindPress: (button, callback) => button.addEventListener('click', callback),
      bindLocalizedTooltip: () => {},
    })
    view.render(row)
    const input = document.getElementById('keyboard-voice-input')
    input.focus()
  })
})

test('preedit keeps the native value, selection, input events and toolbar unchanged', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('before SELECT after')
  const before = await page.evaluate(() => {
    const input = document.getElementById('keyboard-voice-input')
    input.setSelectionRange(7, 13)
    window.events = 0
    input.addEventListener('input', () => window.events++)
    return document.getElementById('keyboard-toolbar-row').getBoundingClientRect().toJSON()
  })
  for (const view of [composing, { ...composing, selectionStart: 0, selectionEnd: 0 }, idle]) {
    await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), view)
    await expect(page.locator('#keyboard-voice-input')).toHaveValue('before SELECT after')
    expect(await page.evaluate(() => {
      const input = document.getElementById('keyboard-voice-input')
      return [input.selectionStart, input.selectionEnd, window.events]
    })).toEqual([7, 13, 0])
    expect(await page.locator('#keyboard-toolbar-row').evaluate(row => row.getBoundingClientRect().toJSON())).toEqual(before)
  }
})

test('whole text shaping and tab width remain fixed as the independent caret moves', async ({ page }) => {
  const dimensions = []
  for (const selectionStart of [0, 2, 5]) {
    await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), { ...composing, preedit: 'office', selectionStart, selectionEnd: selectionStart })
    dimensions.push(await page.locator('#keyboard-ime-preedit').evaluate(tab => ({
      width: tab.getBoundingClientRect().width,
      height: tab.getBoundingClientRect().height,
      paddingLeft: getComputedStyle(tab).paddingLeft,
      paddingRight: getComputedStyle(tab).paddingRight,
      nodes: tab.querySelector('.ime-preedit-content').childNodes.length,
    })))
  }
  expect(dimensions[1]).toEqual(dimensions[0])
  expect(dimensions[2]).toEqual(dimensions[0])
  expect(dimensions[0]).toMatchObject({ height: 34, paddingLeft: '10px', paddingRight: '11px', nodes: 2 })
})

test('long preedit clips only its content and scrolls to the reported caret', async ({ page }) => {
  await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), { ...composing, preedit: 'abcdefghijklmnop'.repeat(20), selectionStart: 320, selectionEnd: 320 })
  const end = await page.locator('#keyboard-ime-preedit').evaluate(tab => ({
    width: tab.offsetWidth, scroll: tab.firstElementChild.scrollLeft,
  }))
  expect(end.scroll).toBeGreaterThan(0)
  expect(end.width).toBeLessThan(650)
  await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), { ...composing, preedit: 'abcdefghijklmnop'.repeat(20), selectionStart: 0, selectionEnd: 0 })
  expect(await page.locator('.ime-preedit-viewport').evaluate(node => node.scrollLeft)).toBe(0)
})

test('a result replaces the current native selection while newer preedit remains active', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('before SELECT after')
  await page.evaluate(view => {
    document.getElementById('keyboard-voice-input').setSelectionRange(7, 13)
    window.__xsoKeyboardIme.showComposition(view)
  }, composing)
  expect(await page.evaluate(() => window.__xsoKeyboardIme.insertText('你'))).toBe('inserted')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('before 你 after')
  await expect(page.locator('#keyboard-ime-preedit')).toBeVisible()
  await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), { ...composing, preedit: 'hao' })
  await expect(page.locator('#keyboard-ime-preedit')).toHaveText('hao')
  expect(await page.evaluate(() => window.messages)).not.toContain('xso-keyboard-ime cancel')
})

test('blur clears immediately and drops results without replay after refocus', async ({ page }) => {
  await page.evaluate(view => {
    window.__xsoKeyboardIme.showComposition(view)
    document.getElementById('keyboard-voice-input').blur()
  }, composing)
  await expect(page.locator('#keyboard-ime-preedit')).toBeHidden()
  expect(await page.evaluate(() => window.__xsoKeyboardIme.insertText('旧提交'))).toBe('focus-lost')
  await page.locator('#keyboard-voice-input').focus()
  await expect(page.locator('#keyboard-ime-preedit')).toBeHidden()
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('')
  expect(await page.evaluate(() => window.__xsoKeyboardIme.insertText('当前'))).toBe('inserted')
})

test('native input reentry cannot turn an external clear into acknowledged insertion', async ({ page }) => {
  await page.evaluate(view => {
    window.__xsoKeyboardIme.showComposition(view)
    document.getElementById('keyboard-voice-input').addEventListener('input', event => {
      event.target.value = ''
    }, { once: true })
  }, composing)
  expect(await page.evaluate(() => window.__xsoKeyboardIme.insertText('被清空'))).toBe('rejected')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('')
  await expect(page.locator('#keyboard-ime-preedit')).toBeHidden()
})

test('external caret movement cancels without restoring an old selection', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('abcdef')
  await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), composing)
  await page.keyboard.press('ArrowLeft')
  await expect(page.locator('#keyboard-ime-preedit')).toBeHidden()
  expect(await page.locator('#keyboard-voice-input').evaluate(input => input.selectionStart)).toBe(5)
  expect(await page.evaluate(() => window.messages)).toContain('xso-keyboard-ime cancel')
})

test('preedit updates preserve undo through repeated native commits', async ({ page }) => {
  await page.keyboard.type('base')
  for (const text of ['你', '好', '啊']) {
    await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), composing)
    await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), { ...composing, preedit: 'next' })
    await page.evaluate(view => window.__xsoKeyboardIme.showComposition(view), idle)
    expect(await page.evaluate(text => window.__xsoKeyboardIme.insertText(text), text)).toBe('inserted')
  }
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('base你好啊')
  await page.keyboard.press('Control+z')
  expect(await page.locator('#keyboard-voice-input').inputValue()).not.toBe('base你好啊')
  expect(await page.locator('#keyboard-voice-input').inputValue()).not.toContain('ni')
  await page.keyboard.press('Control+Shift+z')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('base你好啊')
})

test('missing or replaced composers drop results and never restore cached preedit', async ({ page }) => {
  const outcomes = await page.evaluate(view => {
    window.__xsoKeyboardIme.showComposition(view)
    const old = document.getElementById('keyboard-voice-input')
    const replacement = old.cloneNode()
    replacement.value = 'current'
    old.remove()
    const missing = window.__xsoKeyboardIme.insertText('lost')
    document.getElementById('keyboard-composer-wrap').prepend(replacement)
    replacement.focus()
    replacement.setSelectionRange(3, 3)
    const current = window.__xsoKeyboardIme.insertText('新')
    return [missing, current, old.value]
  }, composing)
  expect(outcomes).toEqual(['focus-lost', 'inserted', ''])
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('cur新rent')
  await expect(page.locator('#keyboard-ime-preedit')).toBeHidden()
})

test('execCommand claiming success without an exact native value and caret is rejected', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('keep')
  const outcome = await page.evaluate(() => {
    document.execCommand = () => true
    return window.__xsoKeyboardIme.insertText('missing')
  })
  expect(outcome).toBe('rejected')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('keep')
})

test('input-handler focus loss rejects acknowledgement without restoring focus or overwriting its edit', async ({ page }) => {
  await page.locator('#keyboard-voice-input').fill('keep')
  await page.evaluate(view => {
    window.__xsoKeyboardIme.showComposition(view)
    document.getElementById('keyboard-voice-input').addEventListener('input', event => {
      event.target.value = 'external'
      document.getElementById('key-F1').focus()
    }, { once: true })
  }, composing)
  expect(await page.evaluate(() => window.__xsoKeyboardIme.insertText('result'))).toBe('rejected')
  await expect(page.locator('#keyboard-voice-input')).toHaveValue('external')
  await expect(page.locator('#key-F1')).toBeFocused()
  await expect(page.locator('#keyboard-ime-preedit')).toBeHidden()
})
