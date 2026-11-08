import { createServer } from 'node:http'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import process from 'node:process'
import { build } from 'esbuild'

const nativeModuleRoot = join(
  process.env.XSOVERLAY_GAME_DIR ?? 'D:/SteamLibrary/steamapps/common/XSOverlay_Beta',
  'XSOverlay_Data/StreamingAssets/Plugins/Applications/UserInterface/Default/Keyboard/js',
)
const nativeModules = new Map(
  ['keyboard-input.js', 'keyboard-audio.js', 'keyboard-speech-composer.js', 'keyboard-speech-view.js', 'keyboard-speech-utils.js', 'keyboard-state.js'].map(name => [
    `/apps/UserInterface/Default/Keyboard/js/${name}`,
    join(nativeModuleRoot, name),
  ]),
)

async function start() {
  const bundle = await build({
    entryPoints: ['src/index.ts'],
    bundle: true,
    write: false,
    format: 'iife',
    loader: { '.css': 'text' },
  })
  const script = bundle.outputFiles[0].text

  createServer((request, response) => {
    if (request.url === '/native-keyboard-available') {
      response.writeHead(200, { 'Content-Type': 'application/json' })
        .end(JSON.stringify([...nativeModules.values()].every(file => existsSync(file))))
      return
    }
    const nativeModule = nativeModules.get(request.url)
    if (nativeModule != null) {
      if (!existsSync(nativeModule)) { response.writeHead(404).end(); return }
      response.writeHead(200, { 'Content-Type': 'text/javascript' }).end(readFileSync(nativeModule))
      return
    }
    if (request.url === '/plugin.js') {
      response.writeHead(200, { 'Content-Type': 'text/javascript' }).end(script)
      return
    }
    if (request.url === '/favicon.ico') {
      response.writeHead(204).end()
      return
    }
    if (request.url !== '/') {
      response.writeHead(404).end()
      return
    }
    response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' })
      .end(`<!doctype html>
<html><head><style>
:root { --animation-time: 150ms; --keyboard-toolbar-inset: 8px; --keyboard-control-size: 48px; --theme-dark: #333; --theme-mid: #444; --theme-hi: #555; --theme-contrasting: white; --theme-accent: cyan; }
body { margin: 10px; }
#keyboard-container { display: flex; flex-direction: column; width: max-content; }
#keyboard-main { display: flex; flex-direction: column; }
.row { display: flex; width: max-content; }
.key { flex: 0 0 auto; box-sizing: border-box; padding: 0; margin: 2px; height: 64px; width: 64px; transition: all 150ms; background: var(--theme-mid); color: white; }
.dark { background: var(--theme-dark); }
.key.pointer-hover { scale: 1.05; }
</style></head><body>
<div id="keyboard-container"><input id="keyboard-voice-input"><button id="keyboard-voice-send">Send</button>
<div id="keyboard-main"><div class="row"><button id="key-F1" class="key" style="width:64px;height:64px;margin:2px">F1</button><button class="key" style="width:600px">A</button></div>
<div class="row" id="bottom"><button id="key-LCONTROL" class="key dark" style="width:96px;height:64px;margin:2px">Ctrl</button><button id="key-LALT" class="key dark" style="width:80px;height:64px;margin:2px">Alt</button><button id="key-SPACE" class="key" style="width:320px;height:64px;margin:2px;transition:all 150ms">Space</button><button id="key-RCONTROL" class="key dark" style="width:96px;height:64px;margin:2px">Ctrl</button><button id="key-RETURN" class="key" style="width:64px;height:64px;margin:2px">Return</button></div></div><div id="keyboard-background"></div></div>
<script>
window.messages = [];
window.vuplex = { postMessage: message => {
  window.messages.push(message);
  if (message === 'xso-keyboard-ime ready') window.__xsoKeyboardIme.setKeyboardFormat('ANSI');
} };
document.getElementById('keyboard-voice-send').addEventListener('click', () => { document.getElementById('keyboard-voice-input').blur(); window.messages.push('native-submit'); });
document.getElementById('key-RETURN').addEventListener('pointerdown', () => document.getElementById('keyboard-voice-send').click());
document.getElementById('key-RETURN').addEventListener('touchstart', () => document.getElementById('keyboard-voice-send').click());
document.getElementById('keyboard-voice-input').addEventListener('keydown', event => { if (event.key === 'Enter') document.getElementById('keyboard-voice-send').click(); });
</script><script src="/plugin.js"></script></body></html>`)
  }).listen(8745, '127.0.0.1')
}

void start()
