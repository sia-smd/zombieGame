import type { SupportedLocale } from './language'
import { getIntlLocale } from './language'

export function formatNumber(
  value: number,
  locale: SupportedLocale,
  options?: Intl.NumberFormatOptions,
): string {
  return new Intl.NumberFormat(getIntlLocale(locale), options).format(value)
}

export function formatDate(
  value: Date | string | number,
  locale: SupportedLocale,
  options?: Intl.DateTimeFormatOptions,
): string {
  const date = value instanceof Date ? value : new Date(value)
  return new Intl.DateTimeFormat(getIntlLocale(locale), options).format(date)
}

export function formatRelativeTime(
  value: Date | string | number,
  locale: SupportedLocale,
): string {
  const date = value instanceof Date ? value : new Date(value)
  const diffSec = Math.round((date.getTime() - Date.now()) / 1000)
  const rtf = new Intl.RelativeTimeFormat(getIntlLocale(locale), { numeric: 'auto' })

  const abs = Math.abs(diffSec)
  if (abs < 60) return rtf.format(diffSec, 'second')
  if (abs < 3600) return rtf.format(Math.round(diffSec / 60), 'minute')
  if (abs < 86400) return rtf.format(Math.round(diffSec / 3600), 'hour')
  return rtf.format(Math.round(diffSec / 86400), 'day')
}
