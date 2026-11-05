// Bundles the injected page script into one ES module the keyboard page imports.
import { build } from 'esbuild'

const outfile = 'dist/keyboard-ime.js'

await build({
  entryPoints: ['src/index.ts'],
  bundle: true,
  format: 'esm',
  target: 'chrome120',
  legalComments: 'none',
  outfile,
})

console.log(`wrote ${outfile}`)
