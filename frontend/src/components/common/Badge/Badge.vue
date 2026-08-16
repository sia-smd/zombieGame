<script setup lang="ts">
withDefaults(
  defineProps<{
    label: string
    variant?: 'default' | 'human' | 'zombie' | 'gold' | 'success' | 'danger' | 'warning' | 'info'
  }>(),
  { variant: 'default' },
)

const variantClass = {
  default: 'bg-border/10 text-text-secondary',
  human: 'bg-success/20 text-success border border-success/30',
  zombie: 'bg-danger/20 text-danger border border-danger/30',
  gold: 'bg-secondary/20 text-secondary border border-secondary/30',
  success: 'bg-success/20 text-success border border-success/30',
  danger: 'bg-danger/20 text-danger border border-danger/30',
  warning: 'bg-warning/20 text-warning border border-warning/30',
  info: 'bg-info/20 text-info border border-info/30',
}
</script>

<template>
  <span
    class="inline-flex items-center rounded-pill px-md py-xs text-caption font-semibold uppercase tracking-wide"
    :class="variantClass[variant]"
  >
    {{ label }}
  </span>
</template>
