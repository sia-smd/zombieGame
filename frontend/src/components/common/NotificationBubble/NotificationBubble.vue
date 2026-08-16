<script setup lang="ts">
defineProps<{
  message: string
  variant?: 'info' | 'success' | 'danger'
}>()

const variantClass = {
  info: 'bg-surface border-border/20 text-text-primary',
  success: 'bg-success/20 border-success/30 text-success',
  danger: 'bg-danger/20 border-danger/30 text-danger',
}
</script>

<template>
  <div
    class="inline-flex max-w-[12rem] rounded-2xl rounded-bl-sm border px-md py-sm text-caption shadow-md animate-ds-fade-up"
    :class="variantClass[variant ?? 'info']"
    role="status"
  >
    {{ message }}
  </div>
</template>
