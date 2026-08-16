<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import RoomMoodGauge from '@/components/room/RoomMoodGauge/RoomMoodGauge.vue'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useRoomStore } from '@/stores/room.store'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { roomService } from '@/services/room.service'
import { BattlePublicAction, WinTeam } from '@/types/enums'
import { images } from '@/assets/images'
import type { BattleSummaryDto } from '@/types/api'

const props = defineProps<{ id: string }>()
const router = useRouter()
const room = useRoomStore()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()

const token = computed(() => {
  const roomToken =
    room.matchId?.toLowerCase() === props.id.toLowerCase()
      ? room.sessionToken
      : null
  return auth.getMatchSession(props.id)?.token ?? roomToken ?? ''
})

const winner = computed(() => {
  if (room.winTeam !== WinTeam.None) return room.winTeam
  return room.state?.winTeam ?? WinTeam.None
})

const liveNavReady = ref(false)
useMatchPhaseNavigation(
  () => props.id,
  () => liveNavReady.value && winner.value === WinTeam.None,
)

const alivePlayers = computed(() => room.players.filter((p) => p.isAlive))
const deadCount = computed(() => room.players.filter((p) => !p.isAlive).length)
const lastBattles = computed(() => room.battleSummaries.slice(-2))

const headline = computed(() => {
  if (winner.value === WinTeam.Humans) return t('matchResult.humansWin')
  if (winner.value === WinTeam.Zombies) return t('matchResult.zombiesWin')
  return t('matchResult.title')
})

const report = computed(() => {
  if (winner.value === WinTeam.Humans) {
    return t('matchResult.reportHumans', {
      alive: alivePlayers.value.length,
      dead: deadCount.value,
      days: room.dayNumber,
    })
  }
  if (winner.value === WinTeam.Zombies) {
    return t('matchResult.reportZombies', {
      alive: alivePlayers.value.length,
      dead: deadCount.value,
      days: room.dayNumber,
    })
  }
  return t('matchResult.reportUnknown')
})

function actionLabel(action: BattlePublicAction) {
  if (action === BattlePublicAction.Action) return t('battleSummary.action')
  if (action === BattlePublicAction.Pass) return t('battleSummary.pass')
  return t('battleSummary.none')
}

function battleLine(b: BattleSummaryDto, index: number) {
  return t('matchResult.battleLine', {
    n: index + 1,
    a: b.player1Name,
    aAction: actionLabel(b.player1Action),
    b: b.player2Name,
    bAction: actionLabel(b.player2Action),
  })
}

onMounted(async () => {
  auth.hydrateFromStorage()
  const session = token.value
  if (!session) return
  const hasFinalState =
    room.matchId?.toLowerCase() === props.id.toLowerCase() &&
    room.state?.matchId?.toLowerCase() === props.id.toLowerCase() &&
    winner.value !== WinTeam.None

  if (hasFinalState) {
    auth.clearMatchSession(props.id)
    void auth.loadProfile()
    return
  }

  try {
    if (room.matchId !== props.id || room.state?.matchId !== props.id) {
      await room.resume(props.id, session)
    }
    await roomService.syncRoom(props.id, session)
    if (winner.value === WinTeam.None) {
      liveNavReady.value = true
      return
    }
    auth.clearMatchSession(props.id)
    void auth.loadProfile()
  } catch (error: unknown) {
    settings.reportError(error)
  }
})

function goHome() {
  auth.clearMatchSession(props.id)
  void auth.loadProfile()
  room.reset()
  router.push('/home')
}
</script>

<template>
  <MobileFrame fullscreen>
    <LoadingOverlay :visible="room.isConnecting" />

    <PageBackdrop
      :src="images.backgrounds.login"
      :overlay-opacity="0.75"
      class="flex min-h-screen flex-col overflow-hidden"
    >
      <div
        class="relative z-10 mx-auto my-auto flex w-full max-w-sm flex-col gap-3 px-4 py-[calc(1rem+var(--safe-top))]"
      >
        <WoodPanel class="result-board px-4 py-5 text-center">
          <p class="text-xs font-bold uppercase tracking-widest text-amber-100/70">
            {{ t('matchResult.dayLabel', { n: room.dayNumber }) }}
          </p>
          <h1 class="result-title mt-1 font-display text-3xl uppercase tracking-widest">
            {{ headline }}
          </h1>
          <p class="mt-3 text-sm leading-relaxed text-white/90">{{ report }}</p>

          <div class="result-stats mt-4 grid grid-cols-2 gap-2 text-left">
            <div class="stat-box">
              <span>{{ t('matchResult.survivors') }}</span>
              <strong class="text-green-400">{{ alivePlayers.length }}</strong>
            </div>
            <div class="stat-box">
              <span>{{ t('matchResult.eliminated') }}</span>
              <strong class="text-red-400">{{ deadCount }}</strong>
            </div>
          </div>

          <div class="mt-4 text-left">
            <RoomMoodGauge :mood="room.roomMood" />
          </div>

          <section v-if="lastBattles.length" class="mt-4 text-left">
            <h2 class="mb-2 text-xs font-extrabold uppercase tracking-wide text-amber-100/80">
              {{ t('matchResult.lastBattles') }}
            </h2>
            <div
              v-for="(battle, i) in lastBattles"
              :key="`${battle.player1Id}-${battle.player2Id}-${i}`"
              class="battle-line"
            >
              {{ battleLine(battle, i) }}
            </div>
          </section>

          <section v-if="alivePlayers.length" class="mt-4 text-left">
            <h2 class="mb-2 text-xs font-extrabold uppercase tracking-wide text-amber-100/80">
              {{ t('matchResult.aliveList') }}
            </h2>
            <p class="text-sm text-white/85">
              {{ alivePlayers.map((p) => p.username).join(' · ') }}
            </p>
          </section>
        </WoodPanel>

        <button type="button" class="home-btn" @click="goHome">
          {{ t('common.returnHome') }}
        </button>
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.result-board {
  border-radius: 1.5rem;
  background-color: rgb(var(--color-game-panel-rgb));
  background-repeat: repeat;
  background-size: 180px auto;
  border: 3px solid rgb(150 100 55 / 0.9);
  box-shadow:
    inset 0 2px 0 rgb(255 255 255 / 0.12),
    0 10px 26px rgb(0 0 0 / 0.5);
}

.result-title {
  color: rgb(var(--color-game-title-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 2px 0 rgb(0 0 0 / 0.7);
}

.stat-box {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.4rem;
  padding: 0.55rem 0.7rem;
  border-radius: 0.75rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  font-size: 0.75rem;
  color: rgb(255 226 170 / 0.85);
}

.stat-box strong {
  font-size: 1.15rem;
}

.battle-line {
  margin-bottom: 0.4rem;
  padding: 0.5rem 0.65rem;
  border-radius: 0.7rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  font-size: 0.75rem;
  color: rgb(var(--color-on-game-rgb));
}

.home-btn {
  min-height: 3.1rem;
  border-radius: var(--radius-pill);
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border: 3px solid rgb(220 255 180 / 0.85);
  box-shadow: 0 6px 0 rgb(0 0 0 / 0.35);
  color: rgb(var(--color-on-game-rgb));
  font-size: 1rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
</style>
