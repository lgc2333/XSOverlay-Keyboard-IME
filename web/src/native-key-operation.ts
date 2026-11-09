/** A native edge completes after its real event and the browser default action. */
export function nativeKeyOperations() {
  let pending: { token: string, key: string, up: boolean, seen: boolean } | null = null
  const channel = new MessageChannel()
  const finish = (success: boolean): void => {
    const operation = pending
    pending = null
    if (operation != null)
      window.vuplex?.postMessage(`xso-keyboard-ime native ${operation.token} ${success ? 'ok' : 'failed'}`)
  }
  channel.port1.onmessage = (event: MessageEvent<string>) => {
    if (pending?.token === event.data) finish(pending.seen)
  }
  const observe = (event: KeyboardEvent): void => {
    if (pending == null || !event.isTrusted || pending.up !== (event.type === 'keyup')) return
    const shifted = '~!@#$%^&*()_+{}|:"<>?'
    const unshifted = '`1234567890-=[]\\;\',./'
    const index = unshifted.indexOf(pending.key)
    const matches = event.key.toLowerCase() === pending.key.toLowerCase()
      || (event.shiftKey && index >= 0 && event.key === shifted[index])
    if (!matches) return
    pending.seen = true
    channel.port2.postMessage(pending.token)
  }
  document.addEventListener('keydown', observe, true)
  document.addEventListener('keyup', observe, true)
  window.addEventListener('pagehide', () => finish(false))
  window.addEventListener('blur', () => finish(false))
  return {
    armNativeKey(token: string, key: string, up: boolean): string {
      if (pending != null) return 'busy'
      pending = { token, key, up, seen: false }
      return 'armed'
    },
    finishNativeKey(token: string): void {
      if (pending?.token === token) channel.port2.postMessage(token)
    },
    cancelNativeKey(): void { finish(false) },
  }
}
