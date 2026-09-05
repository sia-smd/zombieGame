<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Dialog from '@/components/common/Dialog/Dialog.vue'
import Button from '@/components/common/Button/Button.vue'
import { useSettingsStore, type AlertKind } from '@/stores/settings.store'

const settings = useSettingsStore()
const { t } = useI18n()
const confirming = ref(false)

const open = computed(() => settings.alert?.open === true)
const kind = computed<AlertKind>(() => settings.alert?.kind ?? 'error')
const message = computed(() => settings.alert?.message ?? '')

const title = computed(() =>
  kind.value === 'connection'
    ? t('dialog.connectionLostTitle')
    : kind.value === 'update'
      ? t('dialog.updateTitle')
    : t('dialog.errorTitle'),
)

const hint = computed(() =>
  kind.value === 'connection'
    ? t('dialog.connectionLost')
    : kind.value === 'update'
      ? t('dialog.updateMessage')
    : t('dialog.tryAgain'),
)

const confirmLabel = computed(() =>
  kind.value === 'connection'
    ? t('dialog.retry')
    : kind.value === 'update'
      ? t('dialog.reloadNow')
      : t('common.ok'),
)

async function onConfirm() {
  const handler = settings.alert?.onConfirm
  if (!handler) {
    settings.closeAlert()
    return
  }

  confirming.value = true
  try {
    await handler()
    settings.closeAlert()
  } catch {
    // Keep the dialog open so the user can retry again.
  } finally {
    confirming.value = false
  }
}

function onClose() {
  if (kind.value === 'connection' || kind.value === 'update') return
  settings.closeAlert()
}
</script>

<template>
  <Dialog
    :open="open"
    :title="title"
    :persistent="kind === 'connection' || kind === 'update'"
    @close="onClose"
  >
    <p class="text-body text-text-secondary">
      {{ message || hint }}
    </p>
    <p v-if="message && kind === 'error'" class="mt-sm text-caption text-text-muted">
      {{ hint }}
    </p>

    <template #actions>
      <Button
        :variant="kind === 'error' ? 'danger' : 'primary'"
        :loading="confirming"
        block
        @click="onConfirm"
      >
        {{ confirmLabel }}
      </Button>
    </template>
  </Dialog>
</template>
