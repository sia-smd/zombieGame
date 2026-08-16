import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useStorage } from '@vueuse/core'
import type { ToastVariant } from '@/types/design-tokens'
import { translate } from '@/i18n'
import { normalizeAppError } from '@/utils/errors'

export type AlertKind = 'error' | 'connection'

export type AlertDialogState = {
  open: boolean
  kind: AlertKind
  /** Server / action error text. Empty for connection-lost (copy comes from i18n). */
  message: string
  onConfirm: (() => void | Promise<void>) | null
}

export const useSettingsStore = defineStore('settings', () => {
  const soundEnabled = useStorage('zvh_sound', true)
  const musicEnabled = useStorage('zvh_music', true)
  const hapticsEnabled = useStorage('zvh_haptics', true)
  const reducedMotion = useStorage('zvh_reduced_motion', false)

  const toasts = ref<Array<{ id: string; type: ToastVariant; message: string }>>([])
  const alert = ref<AlertDialogState | null>(null)

  function pushToast(type: ToastVariant, message: string) {
    // Errors surface as a blocking dialog (confirm / retry) instead of a fleeting toast.
    if (type === 'error') {
      showError(message)
      return
    }

    const id = crypto.randomUUID()
    toasts.value.push({ id, type, message })
    setTimeout(() => removeToast(id), 4000)
  }

  function removeToast(id: string) {
    toasts.value = toasts.value.filter((t) => t.id !== id)
  }

  function showError(message: string) {
    alert.value = {
      open: true,
      kind: 'error',
      message: message.trim() || translate('errors.unexpected'),
      onConfirm: null,
    }
  }

  function reportError(error: unknown, fallback?: string) {
    const normalized = normalizeAppError(
      error,
      fallback ?? translate('errors.unexpected'),
    )
    if (normalized.kind === 'network') {
      showError(translate('dialog.connectionLost'))
      return normalized
    }
    if (normalized.kind === 'auth') {
      showError(translate('errors.sessionExpired'))
      return normalized
    }
    if (normalized.code?.startsWith('inviteErrors.')) {
      showError(translate(normalized.code))
      return normalized
    }
    showError(normalized.message)
    return normalized
  }

  function showConnectionLost(onRetry?: () => void | Promise<void>) {
    // Avoid stacking multiple connection dialogs while reconnecting.
    if (alert.value?.open && alert.value.kind === 'connection') {
      if (onRetry) alert.value.onConfirm = onRetry
      return
    }

    alert.value = {
      open: true,
      kind: 'connection',
      message: '',
      onConfirm: onRetry ?? null,
    }
  }

  function closeAlert() {
    alert.value = null
  }

  return {
    soundEnabled,
    musicEnabled,
    hapticsEnabled,
    reducedMotion,
    toasts,
    alert,
    pushToast,
    removeToast,
    showError,
    reportError,
    showConnectionLost,
    closeAlert,
  }
})
