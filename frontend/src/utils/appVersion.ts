/** Compare dotted versions like 1.2.3. Returns negative if a < b. */
export function compareVersions(a: string, b: string): number {
  const left = parse(a)
  const right = parse(b)
  const len = Math.max(left.length, right.length)
  for (let i = 0; i < len; i++) {
    const diff = (left[i] ?? 0) - (right[i] ?? 0)
    if (diff !== 0) return diff
  }
  return 0
}

export function isClientSupported(clientVersion: string, minClientVersion: string): boolean {
  return compareVersions(clientVersion, minClientVersion) >= 0
}

function parse(version: string): number[] {
  return version
    .split(/[.+-]/)
    .map((part) => Number.parseInt(part, 10))
    .map((n) => (Number.isFinite(n) ? n : 0))
}

export const APP_VERSION =
  (typeof __APP_VERSION__ !== 'undefined' ? __APP_VERSION__ : import.meta.env.VITE_APP_VERSION) || '1.0.0'
