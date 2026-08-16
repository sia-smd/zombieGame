<script setup lang="ts">
import type { Component } from 'vue'

withDefaults(
  defineProps<{
    icon: Component
    label?: string
    variant?: 'default' | 'secondary' | 'danger' | 'ghost'
    disabled?: boolean
    size?: 'sm' | 'md' | 'lg'
  }>(),
  {
    variant: 'default',
    size: 'md',
  },
)

defineEmits<{ click: [event: MouseEvent] }>()

const variantClass = {
  default: 'bg-border/5 border-border/10 text-text-primary hover:bg-border/10',
  secondary: 'bg-secondary/10 border-secondary/30 text-secondary hover:bg-secondary/20',
  danger: 'bg-danger/10 border-danger/30 text-danger hover:bg-danger/20',
  ghost: 'bg-transparent border-transparent text-text-secondary hover:bg-border/5',
}

const sizeClass = {
  sm: 'h-9 w-9',
  md: 'h-11 w-11',
  lg: 'h-14 w-14',
}
</script>

<template>
  <button
    type="button"
    class="inline-flex items-center justify-center rounded-2xl border transition-all duration-fast active:scale-95 focus-visible:ring-secondary disabled:opacity-disabled"
    :class="[variantClass[variant], sizeClass[size]]"
    :aria-label="label"
    :disabled="disabled"
    @click="$emit('click', $event)"
  >
    <component :is="icon" class="h-5 w-5" />
  </button>
</template>
