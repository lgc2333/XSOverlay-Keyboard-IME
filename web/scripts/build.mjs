// Bundles the injected page script into one ES module the keyboard page imports.
import { build } from 'esbuild'

const outfile = 'dist/keyboard-ime.js'

await build({
  entryPoints: ['src/index.ts'],
  bundle: true,
  format: 'esm',
  target: 'chrome120',
  // The bar keeps its rules in a stylesheet, and the injected page has no asset host,
  // so the stylesheet travels as text inside the bundle. See ADR 0028.
  loader: { '.css': 'text' },
  legalComments: 'none',
  outfile,
})

console.log(`wrote ${outfile}`)
