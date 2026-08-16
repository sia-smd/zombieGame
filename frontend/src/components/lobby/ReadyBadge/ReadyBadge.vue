<script setup lang="ts">
import Badge from '@/components/common/Badge/Badge.vue'
import { useI18n } from 'vue-i18n'

defineProps<{ ready?: boolean }>()

const { t } = useI18n()
</script>

<template>
  <Badge
    :label="ready ? t('common.ready') : t('common.waiting')"
    :variant="ready ? 'success' : 'default'"
  />
</template>
