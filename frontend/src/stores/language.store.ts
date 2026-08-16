import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { i18n, loadLocaleMessages } from '@/i18n'
import { applyDocumentDirection, getDirection, isRtlLocale } from '@/i18n/helpers/direction'
import type { TextDirection } from '@/i18n/helpers/direction'
import {
  LOCALE_DEFINITIONS,
  resolveInitialLocale,
  saveLocale,
  SUPPORTED_LOCALES,
  type LocaleDefinition,
  type SupportedLocale,
} from '@/i18n/helpers/language'

function setGlobalLocale(locale: SupportedLocale) {
  const globalLocale = i18n.global.locale
  if (typeof globalLocale === 'string') {
    i18n.global.locale = locale
  } else {
    globalLocale.value = locale
  }
}

export const useLanguageStore = defineStore('language', () => {
  const language = ref<SupportedLocale>(resolveInitialLocale())
  const isChanging = ref(false)

  const direction = computed<TextDirection>(() => getDirection(language.value))
  const isRTL = computed(() => isRtlLocale(language.value))

  const availableLanguages = computed<LocaleDefinition[]>(() =>
    SUPPORTED_LOCALES.map((code) => LOCALE_DEFINITIONS[code]),
  )

  async function init(): Promise<void> {
    await applyLanguage(language.value, false)
  }

  async function changeLanguage(locale: SupportedLocale): Promise<void> {
    if (locale === language.value) return
    isChanging.value = true
    try {
      await applyLanguage(locale, true)
    } finally {
      isChanging.value = false
    }
  }

  async function applyLanguage(locale: SupportedLocale, persist: boolean): Promise<void> {
    await loadLocaleMessages(locale)
    language.value = locale
    setGlobalLocale(locale)
    applyDocumentDirection(locale)
    if (persist) saveLocale(locale)
  }

  return {
    language,
    direction,
    isRTL,
    isChanging,
    availableLanguages,
    init,
    changeLanguage,
  }
})
