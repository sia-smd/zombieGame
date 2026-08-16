<script setup lang="ts">
import { useSettingsStore } from '@/stores/settings.store'
import type { ToastVariant } from '@/types/design-tokens'

const settings = useSettingsStore()

const typeClass: Record<ToastVariant, string> = {
  info: 'border-border/30 bg-surface shadow-md',
  success: 'border-success/40 bg-success/15 shadow-glow-success',
  warning: 'border-warning/40 bg-warning/15 shadow-glow-warning',
  error: 'border-danger/40 bg-danger/15 shadow-glow-danger',
}
</script>

<template>
  <div class="pointer-events-none fixed inset-x-0 top-[calc(var(--safe-top)+var(--space-lg))] z-toast flex flex-col items-center gap-sm px-lg">
    <div
      v-for="toast in settings.toasts"
      :key="toast.id"
      class="pointer-events-auto flex w-full max-w-mobile items-center gap-md rounded-2xl border px-lg py-md text-body animate-ds-fade-up"
      :class="typeClass[toast.type as ToastVariant] ?? typeClass.info"
      role="status"
    >
      <span class="flex-1 text-text-primary">{{ toast.message }}</span>
      <button
        type="button"
        class="text-text-muted hover:text-text-primary"
        :aria-label="$t('common.close')"
        @click="settings.removeToast(toast.id)"
      >
        ✕
      </button>
    </div>
  </div>
</template>
