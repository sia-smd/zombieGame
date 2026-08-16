<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Dialog from '@/components/common/Dialog/Dialog.vue'
import Button from '@/components/common/Button/Button.vue'

defineProps<{
  open: boolean
  title?: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  variant?: 'danger' | 'primary'
  loading?: boolean
}>()

const emit = defineEmits<{ confirm: []; cancel: []; close: [] }>()
const { t } = useI18n()

function cancel() {
  emit('cancel')
  emit('close')
}
</script>

<template>
  <Dialog :open="open" :title="title" @close="emit('close')">
    <p class="text-body text-text-secondary">{{ message }}</p>
    <template #actions>
      <Button variant="ghost" @click="cancel">
        {{ cancelLabel ?? t('common.cancel') }}
      </Button>
      <Button
        :variant="variant ?? 'primary'"
        :loading="loading"
        @click="emit('confirm')"
      >
        {{ confirmLabel ?? t('common.ok') }}
      </Button>
    </template>
  </Dialog>
</template>
