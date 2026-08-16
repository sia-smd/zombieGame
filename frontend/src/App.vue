<script setup lang="ts">
import { onMounted, onUnmounted, watch } from 'vue'
import { RouterView } from 'vue-router'
import Notification from '@/components/common/Notification/Notification.vue'
import AlertDialog from '@/components/common/AlertDialog/AlertDialog.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import WaitingRoomInviteListener from '@/components/lobby/WaitingRoomInviteListener.vue'
import { useSettingsStore } from '@/stores/settings.store'
import { useRoomStore } from '@/stores/room.store'
import { useUiStore } from '@/stores/ui.store'
import { applyReducedMotionPreference } from '@/composables/useAnimation'
import {
  onHubConnectionStatus,
  type HubName,
  type HubConnectionStatus,
} from '@/services/signalr'
import { createConnectionRecovery } from '@/services/connection-recovery'

const settings = useSettingsStore()
const room = useRoomStore()
const ui = useUiStore()
let stopListening: (() => void) | null = null
const connectionRecovery = createConnectionRecovery({
  room,
  closeConnectionAlert: () => {
    if (settings.alert?.kind === 'connection') settings.closeAlert()
  },
  showConnectionAlert: (retry) => settings.showConnectionLost(retry),
})

function onConnectionStatus(status: HubConnectionStatus, hub: HubName) {
  if (status === 'reconnecting' || status === 'closed') {
    settings.showConnectionLost(() => connectionRecovery.retry(hub))
    return
  }

  if (status === 'reconnected') void connectionRecovery.afterReconnect(hub)
}

onMounted(() => {
  applyReducedMotionPreference(settings.reducedMotion)
  stopListening = onHubConnectionStatus(onConnectionStatus)
})

watch(
  () => settings.reducedMotion,
  (enabled) => applyReducedMotionPreference(enabled),
)

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <RouterView />
  <LoadingOverlay :visible="ui.isBusy" />
  <WaitingRoomInviteListener />
  <Notification />
  <AlertDialog />
</template>
