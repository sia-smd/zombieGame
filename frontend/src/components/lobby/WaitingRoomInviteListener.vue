<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import ConfirmDialog from '@/components/common/ConfirmDialog/ConfirmDialog.vue'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { connectHub } from '@/services/signalr'
import { roomService } from '@/services/room.service'
import { gameService } from '@/services/game.service'
import type { RoomInviteReceivedDto } from '@/types/api'

const router = useRouter()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()

const incoming = ref<RoomInviteReceivedDto | null>(null)
const responding = ref(false)

const message = computed(() => {
  const invite = incoming.value
  if (!invite) return ''
  return t('inviteErrors.received', {
    name: invite.fromUsername,
    room: invite.roomName,
    code: invite.roomCode,
  })
})

onMounted(() => {
  if (auth.isAuthenticated) void connectHub('room').catch(() => undefined)
})

watch(
  () => auth.isAuthenticated,
  (ok) => {
    if (ok) void connectHub('room').catch(() => undefined)
  },
)

const stopReceived = roomService.onRoomInviteReceived((payload) => {
  incoming.value = payload
})

const stopResolved = roomService.onRoomInviteResolved((payload) => {
  if (payload.accepted) {
    settings.pushToast('success', t('inviteErrors.acceptedToast', { name: payload.toUsername }))
  } else {
    settings.pushToast('info', t('inviteErrors.declinedToast', { name: payload.toUsername }))
  }
})

onUnmounted(() => {
  stopReceived()
  stopResolved()
})

async function accept() {
  const invite = incoming.value
  if (!invite || responding.value) return
  responding.value = true
  try {
    const joined = await gameService.acceptRoomInvite(invite.inviteId)
    incoming.value = null
    auth.setMatchSession(joined.matchId, joined.sessionToken)
    await router.push({ name: 'lobby', params: { id: joined.matchId } })
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    responding.value = false
  }
}

async function deny() {
  const invite = incoming.value
  if (!invite || responding.value) return
  responding.value = true
  try {
    await gameService.denyRoomInvite(invite.inviteId)
    incoming.value = null
  } catch (e: unknown) {
    settings.reportError(e)
    incoming.value = null
  } finally {
    responding.value = false
  }
}
</script>

<template>
  <ConfirmDialog
    :open="!!incoming"
    :title="t('inviteErrors.title')"
    :message="message"
    :confirm-label="t('inviteErrors.accept')"
    :cancel-label="t('inviteErrors.deny')"
    :loading="responding"
    variant="primary"
    @confirm="accept"
    @close="deny"
  />
</template>
