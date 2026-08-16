<script setup lang="ts">
import ProgressBar from '@/components/common/ProgressBar/ProgressBar.vue'

defineProps<{
  current: number
  max?: number
  label?: string
}>()
</script>

<template>
  <ProgressBar :value="current" :max="max ?? 100" variant="mana" :label="label" />
</template>
