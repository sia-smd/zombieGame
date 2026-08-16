/** Canonical Iranian mobile: 09xxxxxxxxx */
export function formatIranMobile(value: string): string {
  const digits = value.replace(/\D/g, '')
  if (digits.startsWith('98') && digits.length === 12) return `0${digits.slice(2)}`
  if (digits.length === 10 && digits.startsWith('9')) return `0${digits}`
  return digits
}

export function isIranMobile(value: string): boolean {
  return /^09\d{9}$/.test(formatIranMobile(value))
}
