/// <reference types="vite/client" />

declare global {
  const __APP_VERSION__: string
}

declare module '*.png' {
  const src: string
  export default src
}

declare module '*.webp' {
  const src: string
  export default src
}

import type { MessageSchema } from '@/i18n'
import type { SupportedLocale } from '@/i18n/helpers/language'

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  readonly VITE_HUB_BASE_URL: string
  readonly VITE_APP_VERSION: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare module 'vue-i18n' {
  export interface DefineLocaleMessage extends MessageSchema {}
}

declare module '@vue/runtime-core' {
  interface ComponentCustomProperties {
    $t: (key: string, ...args: unknown[]) => string
  }
}

export type { SupportedLocale }
