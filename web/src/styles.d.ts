/** esbuild hands a stylesheet over as its own text, because of the text loader. */
declare module '*.css' {
  const styles: string
  export default styles
}
