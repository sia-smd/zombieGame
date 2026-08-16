<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ size?: 'sm' | 'md' | 'lg'; label?: string }>()

const { t } = useI18n()
const sizeClass = { sm: 'h-5 w-5', md: 'h-8 w-8', lg: 'h-12 w-12' }
</script>

<template>
  <div class="flex flex-col items-center gap-2">
    <div
      class="animate-spin rounded-full border-2 border-white/10 border-t-gold"
      :class="sizeClass[size ?? 'md']"
      role="status"
      :aria-label="label ?? t('common.loading')"
    />
    <p v-if="label" class="text-sm text-mist">{{ label }}</p>
  </div>
</template>
