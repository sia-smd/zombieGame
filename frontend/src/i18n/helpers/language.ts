export const LANGUAGE_STORAGE_KEY = 'zvh_lang'
export const DEFAULT_LOCALE = 'en' as const

export type SupportedLocale = 'en' | 'fa'

export interface LocaleDefinition {
  code: SupportedLocale
  name: string
  nativeName: string
  flag: string
  dir: 'ltr' | 'rtl'
  intl: string
}

export const SUPPORTED_LOCALES: readonly SupportedLocale[] = ['en', 'fa'] as const

export const LOCALE_DEFINITIONS: Record<SupportedLocale, LocaleDefinition> = {
  en: {
    code: 'en',
    name: 'English',
    nativeName: 'English',
    flag: '🇬🇧',
    dir: 'ltr',
    intl: 'en-US',
  },
  fa: {
    code: 'fa',
    name: 'Persian',
    nativeName: 'فارسی',
    flag: '🇮🇷',
    dir: 'rtl',
    intl: 'fa-IR',
  },
}

export function isSupportedLocale(value: string | null | undefined): value is SupportedLocale {
  return SUPPORTED_LOCALES.includes(value as SupportedLocale)
}

export function getStoredLocale(): SupportedLocale | null {
  const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY)
  return isSupportedLocale(stored) ? stored : null
}

export function saveLocale(locale: SupportedLocale): void {
  localStorage.setItem(LANGUAGE_STORAGE_KEY, locale)
}

export function detectBrowserLocale(): SupportedLocale {
  const candidates = [
    ...navigator.languages,
    navigator.language,
  ].filter(Boolean) as string[]

  for (const tag of candidates) {
    const base = tag.split('-')[0]?.toLowerCase()
    if (isSupportedLocale(base)) return base
  }

  return DEFAULT_LOCALE
}

export function resolveInitialLocale(): SupportedLocale {
  return getStoredLocale() ?? detectBrowserLocale()
}

export function getLocaleDefinition(locale: SupportedLocale): LocaleDefinition {
  return LOCALE_DEFINITIONS[locale]
}

export function getIntlLocale(locale: SupportedLocale): string {
  return LOCALE_DEFINITIONS[locale].intl
}
