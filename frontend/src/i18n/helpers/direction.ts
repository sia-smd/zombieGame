import type { SupportedLocale } from './language'
import { getLocaleDefinition } from './language'

export type TextDirection = 'ltr' | 'rtl'

export function getDirection(locale: SupportedLocale): TextDirection {
  return getLocaleDefinition(locale).dir
}

export function isRtlLocale(locale: SupportedLocale): boolean {
  return getDirection(locale) === 'rtl'
}

export function applyDocumentDirection(locale: SupportedLocale): void {
  const { dir } = getLocaleDefinition(locale)
  const html = document.documentElement
  html.setAttribute('dir', dir)
  html.setAttribute('lang', locale)
}
