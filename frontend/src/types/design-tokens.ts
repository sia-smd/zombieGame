export type ThemeMode = 'dark'

export type ButtonVariant =
  | 'primary'
  | 'secondary'
  | 'ghost'
  | 'danger'
  | 'success'
  | 'outline'

export type ButtonSize = 'sm' | 'md' | 'lg'

export type CardVariant =
  | 'default'
  | 'glass'
  | 'outlined'
  | 'popup'
  | 'game'

export type CardPadding = 'sm' | 'md' | 'lg'

export type CardGlow = 'none' | 'primary' | 'danger' | 'success' | 'warning' | 'secondary'

export type InputVariant = 'default' | 'search'

export type ToastVariant = 'info' | 'success' | 'warning' | 'error'

export const THEME_STORAGE_KEY = 'zvh_theme'

export const BUTTON_VARIANTS: ButtonVariant[] = [
  'primary',
  'secondary',
  'ghost',
  'danger',
  'success',
  'outline',
]

export const CARD_VARIANTS: CardVariant[] = [
  'default',
  'glass',
  'outlined',
  'popup',
  'game',
]
