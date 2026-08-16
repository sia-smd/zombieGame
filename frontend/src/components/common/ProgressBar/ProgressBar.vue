<script setup lang="ts">
defineProps<{
  value: number
  max?: number
  variant?: 'health' | 'mana' | 'xp' | 'timer'
  label?: string
}>()

const variantClass = {
  health: 'from-danger to-primary',
  mana: 'from-info to-info/70',
  xp: 'from-secondary to-warning',
  timer: 'from-success to-success/70',
}
</script>

<template>
  <div class="w-full">
    <div v-if="label" class="mb-xs flex justify-between text-caption text-text-secondary">
      <span>{{ label }}</span>
      <span>{{ value }}{{ max ? ` / ${max}` : '' }}</span>
    </div>
    <div
      class="h-2.5 overflow-hidden rounded-pill bg-bg/50 ring-1 ring-border/10"
      role="progressbar"
      :aria-valuenow="value"
      :aria-valuemin="0"
      :aria-valuemax="max ?? 100"
    >
      <div
        class="h-full rounded-pill bg-gradient-to-r transition-all duration-slow ease-out"
        :class="variantClass[variant ?? 'health']"
        :style="{ width: `${max ? Math.min(100, (value / max) * 100) : value}%` }"
      />
    </div>
  </div>
</template>
