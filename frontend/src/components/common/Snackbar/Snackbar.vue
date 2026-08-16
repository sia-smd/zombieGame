<script setup lang="ts">
import type { ToastVariant } from '@/types/design-tokens'

defineProps<{
  open: boolean
  message: string
  variant?: ToastVariant
  actionLabel?: string
}>()

defineEmits<{ action: []; close: [] }>()

const variantClass: Record<ToastVariant, string> = {
  info: 'bg-surface border-border/20',
  success: 'bg-success/90 border-success',
  warning: 'bg-warning/90 border-warning',
  error: 'bg-danger/90 border-danger',
}
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-transform duration-slow ease-bounce"
      leave-active-class="transition-opacity duration-fast"
      enter-from-class="translate-y-full opacity-0"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-x-lg bottom-[calc(var(--safe-bottom)+var(--space-lg))] z-toast"
        role="status"
      >
        <div
          class="flex items-center gap-md rounded-2xl border px-lg py-md text-body text-text-primary shadow-lg"
          :class="variantClass[variant ?? 'info']"
        >
          <span class="flex-1">{{ message }}</span>
          <button
            v-if="actionLabel"
            type="button"
            class="font-semibold text-secondary"
            @click="$emit('action')"
          >
            {{ actionLabel }}
          </button>
          <button type="button" class="text-text-muted" @click="$emit('close')">✕</button>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
