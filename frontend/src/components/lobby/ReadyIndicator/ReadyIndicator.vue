<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ ready?: boolean }>()

const { t } = useI18n()
</script>

<template>
  <span
    class="inline-block h-3 w-3 rounded-full"
    :class="ready ? 'bg-plague shadow-glow shadow-glow-green animate-pulse-glow' : 'bg-mist/40'"
    :title="ready ? t('common.ready') : t('common.notReady')"
  />
</template>
