<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import BottomBar from '@/components/layout/BottomBar/BottomBar.vue'
import Button from '@/components/common/Button/Button.vue'
import Input from '@/components/common/Input/Input.vue'
import Dialog from '@/components/common/Dialog/Dialog.vue'
import Avatar from '@/components/common/Avatar/Avatar.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import {
  ArrowLeftIcon,
  ShareIcon,
  ClipboardDocumentIcon,
  UserGroupIcon,
} from '@heroicons/vue/24/outline'
import { useRoomStore } from '@/stores/room.store'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { roomService } from '@/services/room.service'
import { gameService } from '@/services/game.service'
import { useRoomSession } from '@/composables/useRoomSession'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useCountdown } from '@/composables/useAnimation'
import { images } from '@/assets/images'
import { playerAvatarUrl } from '@/utils/playerAvatar'
import { RoomPhase } from '@/types/enums'
import { sameUserId } from '@/utils/ids'
import { isE2eHarness } from '@/utils/e2eRoom'

const props = defineProps<{ id: string }>()
const router = useRouter()
const room = useRoomStore()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()
const starting = ref(false)
const leaving = ref(false)
const inviteOpen = ref(false)
const inviteUsername = ref('')
const inviting = ref(false)

const { ready, sessionToken, isBootstrapping, bootstrap } = useRoomSession(() => props.id, 'join')
const stayOnRevealAfterStart = ref(false)
useMatchPhaseNavigation(
  () => props.id,
  () => ready.value && room.currentPhase !== RoomPhase.Lobby && !stayOnRevealAfterStart.value,
)
const maxPlayers = computed(() => room.state?.maxPlayers ?? 8)
const playerCount = computed(() => room.players.length)
const hostId = computed(() => {
  if (room.state?.hostUserId) return room.state.hostUserId
  const sorted = [...room.players].sort((a, b) => a.seatIndex - b.seatIndex)
  return sorted.find((p) => !p.isBot)?.userId ?? null
})
const isHost = computed(() => {
  const uid = auth.resolvedUserId
  if (!uid) return false
  if (hostId.value && sameUserId(hostId.value, uid)) return true
  const sorted = [...room.players].sort((a, b) => a.seatIndex - b.seatIndex)
  const firstHuman = sorted.find((p) => !p.isBot)
  return firstHuman ? sameUserId(firstHuman.userId, uid) : false
})
const isFull = computed(() => playerCount.value >= maxPlayers.value)
const canStart = computed(() => isHost.value && isFull.value && playerCount.value >= 2)
const startHint = computed(() => {
  if (!auth.resolvedUserId) return t('lobby.sessionExpired')
  if (canStart.value) return t('lobby.readyToStart')
  if (isFull.value && !isHost.value) return t('lobby.onlyHostCanStart')
  if (!isFull.value) {
    return t('lobby.needFullRoom', { current: playerCount.value, max: maxPlayers.value })
  }
  return t('lobby.needMinPlayers')
})
const roomCode = computed(() => props.id.replace(/-/g, '').slice(0, 4).toUpperCase())
const slots = computed(() => {
  const list: Array<{
    userId: string
    username: string
    isAlive: boolean
    isBot: boolean
    seatIndex: number
    imageId?: string
    empty?: boolean
  }> = [...room.players]
    .sort((a, b) => a.seatIndex - b.seatIndex)
    .map((p) => ({ ...p, empty: false }))

  while (list.length < maxPlayers.value) {
    list.push({
      userId: `empty-${list.length}`,
      username: '',
      isAlive: true,
      isBot: false,
      seatIndex: list.length,
      empty: true,
    })
  }
  return list.slice(0, maxPlayers.value)
})

const { secondsLeft: botsSecondsLeft } = useCountdown(
  () => room.state?.botsJoinAt,
  () => !isFull.value && !!room.state?.fillWithBots,
)

let pollTimer: ReturnType<typeof setInterval> | null = null
let lastLobbySyncErrorAt = 0

onMounted(async () => {
  if (!(await bootstrap())) return
  if (isE2eHarness()) return
  pollTimer = setInterval(async () => {
    if (!sessionToken.value || room.currentPhase !== RoomPhase.Lobby) return
    try {
      await roomService.syncRoom(props.id, sessionToken.value)
    } catch (error: unknown) {
      const now = Date.now()
      if (now - lastLobbySyncErrorAt < 10_000) return
      lastLobbySyncErrorAt = now
      settings.reportError(error, t('lobby.syncFailed'))
    }
  }, 2000)
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
  // Keep the room hub connection alive for reveal-role and the in-match flow.
})

watch(
  () => room.lastEvent,
  (evt) => {
    if (evt === 'GameStarted') {
      stayOnRevealAfterStart.value = true
      router.replace({ name: 'reveal-role', params: { id: props.id } })
    }
  },
)

async function sendInvite() {
  const username = inviteUsername.value.trim()
  if (!username) {
    settings.pushToast('error', t('inviteErrors.userNotFound'))
    return
  }
  inviting.value = true
  try {
    await gameService.sendRoomInvite({ matchId: props.id, username })
    inviteOpen.value = false
    inviteUsername.value = ''
    settings.pushToast('success', t('inviteErrors.sent'))
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    inviting.value = false
  }
}

async function copyCode() {
  try {
    await navigator.clipboard.writeText(roomCode.value)
    settings.pushToast('success', t('lobby.copied'))
  } catch {
    settings.pushToast('info', roomCode.value)
  }
}

async function startGame() {
  if (!canStart.value || !sessionToken.value) {
    settings.pushToast('info', startHint.value)
    return
  }
  starting.value = true
  try {
    await roomService.startGame(props.id, sessionToken.value)
    stayOnRevealAfterStart.value = true
    router.replace({ name: 'reveal-role', params: { id: props.id } })
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    starting.value = false
  }
}

async function leaveRoom() {
  if (leaving.value) return
  leaving.value = true
  try {
    await roomService.leaveRoom(props.id)
    auth.clearMatchSession(props.id)
    room.reset()
    await router.push({ name: 'rooms' }).catch(() => router.push('/home'))
  } catch (e: unknown) {
    settings.reportError(e)
    auth.clearMatchSession(props.id)
    room.reset()
    await router.push('/home')
  } finally {
    leaving.value = false
  }
}

function isEmptySlot(slot: { empty?: boolean; userId: string }) {
  return !!slot.empty || slot.userId.startsWith('empty-')
}
</script>

<template>
  <MobileFrame fullscreen>
    <LoadingOverlay :visible="isBootstrapping || room.isConnecting" :message="t('lobby.entering')" />
    <PageBackdrop :src="images.backgrounds.login" :overlay-opacity="0.55" class="flex min-h-screen flex-col">
      <div class="relative z-10 flex min-h-screen flex-col">
        <WoodPanel
          as="header"
          texture="wood02"
          class="lobby-header-bar mx-3 mt-[calc(0.5rem+var(--safe-top))] flex items-center justify-between px-3 py-2"
        >
          <IconButton :icon="ArrowLeftIcon" :label="t('common.back')" :disabled="leaving" @click="leaveRoom" />
          <IconButton :icon="ShareIcon" :label="t('lobby.share')" @click="copyCode" />
        </WoodPanel>

        <main class="flex flex-1 flex-col overflow-y-auto px-4 pb-4">
          <h1 class="wait-title mt-1 text-center font-display text-3xl uppercase tracking-widest">
            {{ t('lobby.waitingRoom') }}
          </h1>

          <div class="wait-bar mt-4 flex items-center justify-between gap-2 px-3 py-2">
            <button type="button" class="inline-flex items-center gap-1.5 text-sm font-bold text-on-game" @click="copyCode">
              <span>#{{ roomCode }}</span>
              <ClipboardDocumentIcon class="h-4 w-4 text-on-game/80" />
            </button>
            <span class="rounded-pill bg-game-accent/90 px-3 py-0.5 text-xs font-bold uppercase text-on-game">
              {{ t('lobby.waiting') }}
            </span>
            <span class="inline-flex items-center gap-1 text-sm font-semibold text-on-game">
              <UserGroupIcon class="h-4 w-4" />
              {{ playerCount }} / {{ maxPlayers }}
            </span>
          </div>

          <p v-if="isHost" class="mt-3 text-center text-xs text-secondary">
            ♔ {{ t('lobby.youAreHost') }}
          </p>

          <div class="mt-4 grid grid-cols-4 gap-3">
            <div v-for="slot in slots" :key="slot.userId" class="flex flex-col items-center gap-1">
              <template v-if="isEmptySlot(slot)">
                <div class="flex h-14 w-14 items-center justify-center rounded-full border-2 border-dashed border-on-game/35 text-xl text-on-game/50">
                  +
                </div>
                <span class="text-[10px] text-on-game/55">{{ t('lobby.emptySlot') }}</span>
              </template>
              <template v-else>
                <Avatar
                  :name="slot.username"
                  :image-url="playerAvatarUrl(slot.imageId)"
                  size="md"
                  :ring="hostId === slot.userId ? 'success' : 'none'"
                />
                <p class="max-w-[4.5rem] truncate text-[10px] font-semibold text-on-game">{{ slot.username }}</p>
                <span
                  v-if="hostId === slot.userId"
                  class="rounded bg-secondary/90 px-1.5 text-[9px] font-bold uppercase text-text-inverse"
                >
                  {{ t('common.host') }}
                </span>
                <span
                  v-else-if="slot.isBot"
                  class="rounded bg-on-game/20 px-1.5 text-[9px] font-bold uppercase text-on-game/80"
                >
                  {{ t('common.bot') }}
                </span>
              </template>
            </div>
          </div>

          <div class="mt-5 text-center">
            <p class="text-sm text-on-game/90">{{ t('lobby.waitingForPlayers') }}</p>
            <p v-if="botsSecondsLeft !== null" class="mt-1 text-xs text-secondary">
              {{ t('lobby.botsJoinIn', { n: botsSecondsLeft }) }}
            </p>
          </div>

          <div class="mt-4 space-y-3">
            <Button
              block
              size="lg"
              variant="success"
              class="wait-start uppercase tracking-wide"
              :disabled="!canStart"
              :loading="starting"
              @click="startGame"
            >
              {{ t('lobby.startGame') }}
            </Button>
            <Button block variant="secondary" class="wait-invite" @click="inviteOpen = true">
              {{ t('lobby.invitePlayers') }}
            </Button>
            <Button
              block
              variant="danger"
              :loading="leaving"
              :disabled="starting"
              @click="leaveRoom"
            >
              {{ t('lobby.leaveRoom') }}
            </Button>
            <p class="text-center text-xs text-on-game/65">
              {{ startHint }}
            </p>
          </div>
        </main>

        <BottomBar wood-texture />
      </div>
    </PageBackdrop>
    <Dialog :open="inviteOpen" :title="t('lobby.invitePlayers')" surface="wood" align="center" @close="inviteOpen = false">
      <p class="mb-3 text-sm text-on-game/80">{{ t('inviteErrors.usernameHint') }}</p>
      <Input
        v-model="inviteUsername"
        :placeholder="t('login.username')"
        autocomplete="username"
        @keyup.enter="sendInvite"
      />
      <template #actions>
        <Button variant="ghost" @click="inviteOpen = false">{{ t('common.cancel') }}</Button>
        <Button :loading="inviting" @click="sendInvite">{{ t('common.send') }}</Button>
      </template>
    </Dialog>
  </MobileFrame>
</template>

<style scoped>
.lobby-header-bar {
  border-radius: var(--radius-3xl);
  background-repeat: repeat-x;
  background-position: center;
  background-size: auto 100%;
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  box-shadow:
    0 4px 0 rgb(0 0 0 / 0.35),
    inset 0 1px 0 rgb(var(--color-on-game-rgb) / 0.12);
}

.wait-title {
  color: rgb(var(--color-game-accent-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 3px 0 rgb(0 0 0 / 0.8),
    0 6px 14px rgb(0 0 0 / 0.45);
}

.wait-bar {
  border-radius: 1rem;
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-wood-start-rgb)) 0%,
    rgb(var(--color-game-wood-end-rgb)) 100%
  );
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.85);
  box-shadow: 0 4px 0 rgb(0 0 0 / 0.35);
}

.wait-start {
  border-width: 3px !important;
  border-color: rgb(var(--color-game-cta-border-rgb) / 0.85) !important;
  border-radius: var(--radius-pill) !important;
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.35),
    0 12px 24px rgb(var(--color-success-rgb) / 0.35) !important;
}

.wait-start:disabled {
  opacity: 0.55;
  filter: grayscale(0.2);
}

.wait-invite {
  border-radius: 1rem !important;
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-wood-start-rgb)) 0%,
    rgb(var(--color-game-wood-end-rgb)) 100%
  ) !important;
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.85) !important;
}
</style>
