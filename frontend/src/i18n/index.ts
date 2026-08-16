import { createI18n, type I18n } from 'vue-i18n'
import type { SupportedLocale } from './helpers/language'
import {
  DEFAULT_LOCALE,
  resolveInitialLocale,
  SUPPORTED_LOCALES,
} from './helpers/language'
import en from './locales/en.json'

export type MessageSchema = typeof en

const loadedLocales = new Set<SupportedLocale>([DEFAULT_LOCALE])

const localeLoaders: Record<SupportedLocale, () => Promise<{ default: MessageSchema }>> = {
  en: () => Promise.resolve({ default: en }),
  fa: () => import('./locales/fa.json'),
}

export const i18n: I18n = createI18n({
  legacy: false,
  locale: DEFAULT_LOCALE,
  fallbackLocale: DEFAULT_LOCALE,
  messages: { en },
  pluralRules: {
    fa: (choice) => (choice === 0 ? 0 : choice === 1 ? 1 : 2),
  },
})

function setGlobalLocale(locale: SupportedLocale) {
  const globalLocale = i18n.global.locale
  if (typeof globalLocale === 'string') {
    i18n.global.locale = locale
  } else {
    globalLocale.value = locale
  }
}

export async function loadLocaleMessages(locale: SupportedLocale): Promise<void> {
  if (loadedLocales.has(locale)) return

  const loader = localeLoaders[locale]
  if (!loader) return

  const module = await loader()
  i18n.global.setLocaleMessage(locale, module.default)
  loadedLocales.add(locale)
}

export async function setupI18n(): Promise<I18n> {
  const initial = resolveInitialLocale()
  await loadLocaleMessages(initial)
  setGlobalLocale(initial)
  return i18n
}

export function isLocaleLoaded(locale: SupportedLocale): boolean {
  return loadedLocales.has(locale)
}

export function translate(key: string, ...args: unknown[]): string {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return (i18n.global.t as any)(key, ...args)
}

export { SUPPORTED_LOCALES, DEFAULT_LOCALE }
