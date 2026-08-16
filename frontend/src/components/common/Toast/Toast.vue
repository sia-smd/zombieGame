<script setup lang="ts">
import type { ToastVariant } from '@/types/design-tokens'
import { useSettingsStore } from '@/stores/settings.store'

defineProps<{
  type?: ToastVariant
  message: string
  id: string
}>()

const settings = useSettingsStore()

const typeClass: Record<ToastVariant, string> = {
  info: 'border-border/20 bg-surface/95',
  success: 'border-success/40 bg-success/10',
  warning: 'border-warning/40 bg-warning/10',
  error: 'border-danger/40 bg-danger/10',
}
</script>

<template>
  <div
    class="pointer-events-auto flex items-center gap-md rounded-2xl border px-lg py-md text-body text-text-primary shadow-card backdrop-blur animate-ds-fade-up"
    :class="typeClass[type ?? 'info']"
    role="status"
    :aria-live="type === 'error' ? 'assertive' : 'polite'"
  >
    <span class="flex-1">{{ message }}</span>
    <button
      type="button"
      class="text-text-muted transition-colors hover:text-text-primary focus-visible:ring-secondary"
      :aria-label="$t('common.close')"
      @click="settings.removeToast(id)"
    >
      ✕
    </button>
  </div>
</template>
