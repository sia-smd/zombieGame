<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import RoomPlayerTile from '@/components/room/RoomPlayerTile/RoomPlayerTile.vue'
import RoomChat from '@/components/room/RoomChat/RoomChat.vue'
import BattleInvitationDialog from '@/components/room/BattleInvitationDialog/BattleInvitationDialog.vue'
import PhaseCountdownDialog from '@/components/room/PhaseCountdownDialog/PhaseCountdownDialog.vue'
import RoomMoodGauge from '@/components/room/RoomMoodGauge/RoomMoodGauge.vue'
import {
  ArrowLeftIcon,
  ChatBubbleLeftRightIcon,
  UserGroupIcon,
} from '@heroicons/vue/24/outline'
import { useRoomStore } from '@/stores/room.store'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { roomService } from '@/services/room.service'
import { useGameLabels, useLocaleFormat } from '@/composables/useGameLabels'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useRoomSession } from '@/composables/useRoomSession'
import { useCountdown } from '@/composables/useAnimation'
import { DayEventType, RoomPhase } from '@/types/enums'
import { sameUserId } from '@/utils/ids'
import { getDayEventImage, getDayEventPublicImage } from '@/utils/imageAssets'
import { images } from '@/assets/images'

const props = defineProps<{ id: string }>()

const router = useRouter()
const room = useRoomStore()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()
const { roomPhaseLabel } = useGameLabels()
const { formatLocaleNumber } = useLocaleFormat()

type RoomTab = 'players' | 'chat'

const activeTab = ref<RoomTab>('players')
const inviting = ref(false)
const responding = ref(false)

const myId = computed(() => auth.resolvedUserId)
const { ready, sessionToken, isBootstrapping, bootstrap } = useRoomSession(
  () => props.id,
  'resume',
)
useMatchPhaseNavigation(() => props.id, ready)
const roomCode = computed(() => props.id.replace(/-/g, '').slice(0, 4).toUpperCase())
const coinCount = computed(() => auth.coinCount)
const aliveCount = computed(() => room.players.filter((p) => p.isAlive).length)
const orderedPlayers = computed(() => [...room.players].sort((a, b) => a.seatIndex - b.seatIndex))

const incomingInvitation = computed(() => room.incomingInvitation(myId.value))
const outgoingInvitation = computed(() => room.outgoingInvitation(myId.value))
const invitableIds = computed(() => room.invitableIds(myId.value))

const { secondsLeft: phaseSecondsLeft, clock: phaseClock } = useCountdown(
  () => room.state?.phaseEndsAt,
  true,
  () => room.state?.phaseSecondsRemaining,
  () => room.snapshotReceivedAt,
)

const canFindOpponent = computed(
  () =>
    room.currentPhase === RoomPhase.OpponentSelection &&
    !outgoingInvitation.value &&
    !incomingInvitation.value &&
    invitableIds.value.length > 0,
)

/** Hide when countdown hits 0 even if a late/stale DayStart phase update lags behind. */
const showBattlePrepDialog = computed(
  () =>
    room.currentPhase === RoomPhase.BattlePreparation &&
    (phaseSecondsLeft.value === null || phaseSecondsLeft.value > 0),
)
const showDayStartDialog = computed(
  () =>
    room.currentPhase === RoomPhase.DayStart &&
    (phaseSecondsLeft.value === null || phaseSecondsLeft.value > 0),
)

const dayEvent = computed(() => room.currentDayEvent)
const dayEventImage = computed(() => getDayEventPublicImage(dayEvent.value))
const dayEventFallbackImage = computed(() => getDayEventImage(dayEvent.value))
const dayEventHint = computed(() => {
  switch (dayEvent.value) {
    case DayEventType.SunnyDay:
      return t('dayEvent.sunny')
    case DayEventType.Storm:
      return t('dayEvent.storm')
    default:
      return t('dayEvent.normal')
  }
})

const hintMessage = computed(() => {
  if (room.currentPhase !== RoomPhase.OpponentSelection) return roomPhaseLabel(room.currentPhase)
  if (incomingInvitation.value) return t('invitation.question')
  if (outgoingInvitation.value) return t('room.waitingAnswer')

  const me = room.players.find((p) => sameUserId(p.userId, myId.value))
  if (me?.isPaired) return t('room.status.inBattle')
  if (me?.hasSentInvitationToday) return t('room.alreadyInvitedToday')
  if (!invitableIds.value.length) return t('room.noOpponents')
  return t('room.pickOpponent')
})

onMounted(async () => {
  void auth.loadProfile()
  await bootstrap()
})

watch(phaseSecondsLeft, async (seconds) => {
  if (seconds !== 0) return
  if (
    room.currentPhase === RoomPhase.DayStart ||
    room.currentPhase === RoomPhase.BattlePreparation
  ) {
    try {
      await room.syncNow()
    } catch (error: unknown) {
      settings.reportError(error)
    }
  }
})

async function invite(targetUserId: string) {
  if (inviting.value || !sessionToken.value) return
  inviting.value = true
  try {
    await roomService.sendInvitation(props.id, sessionToken.value, targetUserId)
    const target = room.players.find((p) => sameUserId(p.userId, targetUserId))
    settings.pushToast('success', t('invitation.sentTo', { name: target?.username ?? '' }))
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    inviting.value = false
  }
}

function findOpponent() {
  const candidates = invitableIds.value
  if (!candidates.length) {
    settings.pushToast('info', t('room.noOpponents'))
    return
  }
  invite(candidates[Math.floor(Math.random() * candidates.length)])
}

async function respond(invitationId: string, accept: boolean) {
  if (responding.value || !sessionToken.value) return
  responding.value = true
  try {
    await roomService.respondInvitation(props.id, sessionToken.value, invitationId, accept)
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    responding.value = false
  }
}

async function sendChat(text: string) {
  if (!sessionToken.value) return
  try {
    await roomService.sendChat(props.id, sessionToken.value, text)
  } catch (e: unknown) {
    settings.reportError(e)
  }
}
</script>

<template>
  <MobileFrame fullscreen>
    <LoadingOverlay :visible="isBootstrapping || room.isConnecting" :message="t('game.syncing')" />

    <PageBackdrop
      :src="images.backgrounds.login"
      :overlay-opacity="0.6"
      class="flex h-screen flex-col overflow-hidden"
    >
      <div class="relative z-10 flex h-full flex-col px-3 pt-[calc(0.5rem+var(--safe-top))]">
        <WoodPanel
          as="header"
          texture="wood02"
          class="room-header flex flex-shrink-0 items-center gap-2 px-2 py-1.5"
        >
          <IconButton :icon="ArrowLeftIcon" :label="t('common.back')" @click="router.push('/home')" />
          <h1 class="room-title min-w-0 flex-1 truncate text-center font-display text-lg">
            {{ room.state?.roomName ?? t('common.room') }}
          </h1>
          <span class="room-coins flex flex-shrink-0 items-center gap-1 px-2 py-0.5">
            <img :src="images.ui.coin" alt="" class="h-4 w-4" aria-hidden="true" />
            <span class="text-xs font-bold text-white">{{ formatLocaleNumber(coinCount) }}</span>
          </span>
        </WoodPanel>

        <WoodPanel
          as="section"
          class="room-info mt-2 flex-shrink-0 px-3 py-1.5 text-center"
        >
          <p class="room-info__line">
            {{ t('room.code') }}: <span class="room-info__value">{{ roomCode }}</span>
          </p>
          <p class="room-info__line">{{ t('room.day', { n: room.dayNumber }) }}</p>
          <p class="room-info__line">
            {{ t('room.phase') }}:
            <span class="room-info__value">{{ roomPhaseLabel(room.currentPhase) }}</span>
          </p>
          <p class="room-info__line">{{ t('room.playersAlive', { n: aliveCount }) }}</p>
          <p class="room-clock">{{ phaseClock }}</p>
          <div class="mt-2">
            <RoomMoodGauge :mood="room.roomMood" compact />
          </div>
        </WoodPanel>

        <div class="mt-2 min-h-0 flex-1 overflow-hidden">
          <div v-if="activeTab === 'players'" class="grid h-full grid-cols-3 content-start gap-1.5">
            <RoomPlayerTile
              v-for="player in orderedPlayers"
              :key="player.userId"
              :player="player"
              :status="room.playerStatus(player)"
              :can-invite="room.canInvite(myId, player.userId) && !inviting"
              :is-self="sameUserId(player.userId, myId)"
              @invite="invite"
            />
          </div>

          <WoodPanel
            v-else
            class="room-panel flex h-full flex-col p-2"
          >
            <RoomChat :messages="room.chatMessages" :my-user-id="myId" @send="sendChat" />
          </WoodPanel>
        </div>

        <p class="room-hint flex-shrink-0 py-1 text-center">{{ hintMessage }}</p>

        <WoodPanel
          as="nav"
          texture="wood02"
          class="room-nav flex flex-shrink-0 items-center gap-2 px-2 py-2 pb-[calc(0.5rem+var(--safe-bottom))]"
        >
          <button
            type="button"
            class="room-nav__tab"
            :class="{ 'room-nav__tab--active': activeTab === 'players' }"
            @click="activeTab = 'players'"
          >
            <UserGroupIcon class="h-5 w-5" />
            {{ t('room.players') }}
          </button>

          <button
            type="button"
            class="room-nav__cta"
            :disabled="!canFindOpponent || inviting"
            @click="findOpponent"
          >
            {{ t('room.findOpponent') }}
          </button>

          <button
            type="button"
            class="room-nav__tab"
            :class="{ 'room-nav__tab--active': activeTab === 'chat' }"
            @click="activeTab = 'chat'"
          >
            <ChatBubbleLeftRightIcon class="h-5 w-5" />
            {{ t('room.chat') }}
          </button>
        </WoodPanel>
      </div>
    </PageBackdrop>

    <BattleInvitationDialog
      :invitation="incomingInvitation"
      :loading="responding"
      @accept="respond($event, true)"
      @reject="respond($event, false)"
    />

    <PhaseCountdownDialog
      :open="showBattlePrepDialog"
      :title="t('phaseDialog.battlePrepTitle')"
      :subtitle="t('phaseDialog.battlePrepHint')"
      :ends-at="room.state?.phaseEndsAt"
      :remaining-seconds="room.state?.phaseSecondsRemaining"
      :remaining-synced-at="room.snapshotReceivedAt"
    />
    <PhaseCountdownDialog
      :open="showDayStartDialog"
      :title="t('phaseDialog.dayStartTitle', { n: room.dayNumber })"
      :subtitle="dayEventHint"
      :image-src="dayEventImage"
      :image-fallback-src="dayEventFallbackImage"
      :ends-at="room.state?.phaseEndsAt"
      :remaining-seconds="room.state?.phaseSecondsRemaining"
      :remaining-synced-at="room.snapshotReceivedAt"
    />
  </MobileFrame>
</template>

<style scoped>
.room-header {
  border-radius: var(--radius-3xl);
  background-repeat: repeat-x;
  background-position: center;
  background-size: 220px 100%;
  background-color: rgb(var(--color-game-panel-rgb));
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  box-shadow:
    0 4px 0 rgb(0 0 0 / 0.35),
    inset 0 1px 0 rgb(255 255 255 / 0.12);
}

.room-title {
  color: rgb(var(--color-game-title-rgb));
  text-shadow: 0 2px 0 rgb(0 0 0 / 0.7);
}

.room-coins {
  border-radius: var(--radius-pill);
  background: rgb(20 10 6 / 0.65);
  border: 1px solid rgb(180 130 70 / 0.35);
}

.room-info,
.room-panel {
  border-radius: 0.9rem;
  background-repeat: repeat;
  background-size: 180px auto;
  background-color: rgb(var(--color-game-panel-rgb));
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.75);
  box-shadow: inset 0 0 0 1px rgb(255 255 255 / 0.06);
}

.room-info__line {
  font-size: 0.7rem;
  font-weight: 600;
  line-height: 1.4;
  color: rgb(255 255 255 / 0.85);
}

.room-info__value {
  font-weight: 800;
  color: rgb(var(--color-game-title-rgb));
}

.room-clock {
  font-size: 1.05rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  color: rgb(var(--color-on-game-rgb));
  text-shadow: 0 2px 0 rgb(0 0 0 / 0.6);
}

.room-hint {
  font-size: 0.68rem;
  font-weight: 600;
  color: rgb(255 255 255 / 0.7);
}

.room-nav {
  border-radius: var(--radius-3xl);
  background-repeat: repeat-x;
  background-position: center;
  background-size: 220px 100%;
  background-color: rgb(var(--color-game-panel-rgb));
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.1);
}

.room-nav__tab {
  display: inline-flex;
  min-width: 3.75rem;
  flex-direction: column;
  align-items: center;
  gap: 0.15rem;
  border-radius: 0.75rem;
  padding: 0.25rem 0.4rem;
  font-size: 0.6rem;
  font-weight: 700;
  color: rgb(255 255 255 / 0.7);
}

.room-nav__tab--active {
  color: rgb(var(--color-game-cta-border-rgb));
  text-shadow: 0 0 10px rgb(124 252 0 / 0.55);
}

.room-nav__cta {
  flex: 1;
  min-height: 2.6rem;
  border-radius: var(--radius-pill);
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border: 3px solid rgb(220 255 180 / 0.85);
  font-size: 0.82rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgb(var(--color-on-game-rgb));
  box-shadow:
    0 5px 0 rgb(0 0 0 / 0.35),
    0 8px 18px rgb(34 197 94 / 0.28);
}

.room-nav__cta:disabled {
  opacity: 0.5;
  filter: grayscale(0.25);
}
</style>
