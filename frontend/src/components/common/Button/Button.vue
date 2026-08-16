<script setup lang="ts">
import type { Component } from 'vue'
import type { ButtonSize, ButtonVariant } from '@/types/design-tokens'

withDefaults(
  defineProps<{
    variant?: ButtonVariant
    size?: ButtonSize
    disabled?: boolean
    loading?: boolean
    block?: boolean
    icon?: Component
    type?: 'button' | 'submit' | 'reset'
    ariaLabel?: string
  }>(),
  {
    variant: 'primary',
    size: 'md',
    type: 'button',
  },
)

defineEmits<{ click: [event: MouseEvent] }>()

const variantClass: Record<ButtonVariant, string> = {
  primary:
    'bg-gradient-to-b from-primary to-danger text-text-primary border-primary/30 shadow-glow-primary hover:brightness-110',
  secondary:
    'bg-gradient-to-b from-card to-surface text-secondary border-secondary/20 shadow-glow-warning hover:brightness-110',
  ghost:
    'bg-border/5 text-text-secondary border-border/10 hover:bg-border/10',
  danger:
    'bg-gradient-to-b from-danger to-primary text-text-primary border-danger/40 shadow-glow-danger hover:brightness-110',
  success:
    'bg-gradient-to-b from-game-cta-start to-game-cta-end text-on-game border-game-cta-border shadow-[0_6px_0_rgb(0_0_0_/_0.25)] hover:brightness-110',
  outline:
    'bg-transparent text-text-primary border-border/20 hover:bg-border/5',
}

const sizeClass: Record<ButtonSize, string> = {
  sm: 'h-9 px-xl text-body rounded-2xl',
  md: 'h-11 px-xl text-body rounded-2xl',
  lg: 'h-14 px-2xl text-subtitle rounded-pill font-semibold',
}
</script>

<template>
  <button
    :type="type"
    class="inline-flex items-center justify-center gap-sm border font-body transition-all duration-fast ease-out active:scale-[0.97] focus-visible:ring-secondary disabled:pointer-events-none disabled:opacity-disabled"
    :class="[variantClass[variant], sizeClass[size], block ? 'w-full' : '']"
    :disabled="disabled || loading"
    :aria-label="ariaLabel"
    :aria-busy="loading"
    @click="$emit('click', $event)"
  >
    <span
      v-if="loading"
      class="h-4 w-4 animate-ds-spin rounded-circle border-2 border-text-primary/30 border-t-text-primary"
      aria-hidden="true"
    />
    <component :is="icon" v-else-if="icon" class="h-5 w-5 shrink-0" aria-hidden="true" />
    <slot />
  </button>
</template>
