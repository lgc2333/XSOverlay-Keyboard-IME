/** Uses the duration of XSOverlay's native key animations. */
export function keyAnimationDuration(): number {
  const value = getComputedStyle(document.documentElement).getPropertyValue('--animation-time').trim()
  const match = /^([\d.]+)\s*(ms|s)?$/i.exec(value)
  const number = match == null ? Number.NaN : Number(match[1])
  if (!Number.isFinite(number)) return 150
  return match?.[2]?.toLowerCase() === 's' ? number * 1000 : number
}

export function animateKey(key: HTMLElement): void {
  key.animate([{ scale: '1.05' }, { scale: '0.95' }, { scale: '1.05' }], {
    duration: keyAnimationDuration(), easing: 'linear', fill: 'none',
  })
}
