import { defineStore } from 'pinia'
import { useStorage } from '@vueuse/core'
import type { ThemeMode } from '@/types/design-tokens'
import { THEME_STORAGE_KEY } from '@/types/design-tokens'

function applyDarkThemeToDocument() {
  document.documentElement.setAttribute('data-theme', 'dark')
  document.documentElement.style.colorScheme = 'dark'
}

export const useThemeStore = defineStore('theme', () => {
  const theme = useStorage<ThemeMode>(THEME_STORAGE_KEY, 'dark')

  function init() {
    // The product is intentionally dark-only; overwrite stale persisted light values.
    theme.value = 'dark'
    applyDarkThemeToDocument()
  }

  return {
    theme,
    init,
  }
})
