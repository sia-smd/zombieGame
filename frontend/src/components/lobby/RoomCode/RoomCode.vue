<script setup lang="ts">
import { useI18n } from 'vue-i18n'

defineProps<{ code: string }>()

const { t } = useI18n()
</script>

<template>
  <div class="inline-flex items-center gap-2 rounded-2xl border border-gold/30 bg-gold/10 px-4 py-2">
    <span class="text-xs uppercase tracking-widest text-mist">{{ t('common.room') }}</span>
    <span class="font-mono text-lg font-bold tracking-[0.2em] text-gold">{{ code }}</span>
  </div>
</template>
