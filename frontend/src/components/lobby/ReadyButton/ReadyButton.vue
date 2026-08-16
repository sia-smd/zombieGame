<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Button from '@/components/common/Button/Button.vue'

defineProps<{
  ready?: boolean
  loading?: boolean
  disabled?: boolean
}>()

defineEmits<{ ready: [] }>()

const { t } = useI18n()
</script>

<template>
  <Button
    block
    size="lg"
    :variant="ready ? 'secondary' : 'primary'"
    :loading="loading"
    :disabled="disabled"
    @click="$emit('ready')"
  >
    {{ ready ? t('common.waiting') : t('lobby.ready') }}
  </Button>
</template>
