<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import RoleBadge from '@/components/battle/RoleBadge/RoleBadge.vue'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useRoomStore } from '@/stores/room.store'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { roomService } from '@/services/room.service'
import { BattlePublicAction, PlayerRole, WinTeam } from '@/types/enums'
import { images } from '@/assets/images'
import type { BattleSummaryDto } from '@/types/api'
import { tryApplyE2eRoomState } from '@/utils/e2eRoom'
import { playerAvatarUrl } from '@/utils/playerAvatar'

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
const rosterPlayers = computed(() =>
  [...room.players].sort((a, b) => a.seatIndex - b.seatIndex),
)

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

function hasRevealedRole(role?: PlayerRole | null) {
  return role !== undefined && role !== null && role !== PlayerRole.Unknown
}

onMounted(async () => {
  auth.hydrateFromStorage()
  const session = token.value
  if (!session) return
  if (tryApplyE2eRoomState(room, props.id, session) && winner.value !== WinTeam.None) {
    auth.clearMatchSession(props.id)
    void auth.loadProfile()
    return
  }
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
        <WoodPanel class="result-board max-h-[calc(100vh-6rem)] overflow-y-auto px-4 py-5 text-center">
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

          <section v-if="rosterPlayers.length" class="mt-4 text-left">
            <h2 class="mb-2 text-xs font-extrabold uppercase tracking-wide text-amber-100/80">
              {{ t('matchResult.fullRoster') }}
            </h2>
            <div class="roster-head">
              <span>{{ t('matchResult.colPlayer') }}</span>
              <span>{{ t('matchResult.colRole') }}</span>
              <span>{{ t('matchResult.colStatus') }}</span>
            </div>
            <div
              v-for="player in rosterPlayers"
              :key="player.userId"
              class="roster-row"
              :class="{ 'roster-row--dead': !player.isAlive }"
            >
              <div class="flex min-w-0 items-center gap-2">
                <div class="roster-avatar">
                  <img :src="playerAvatarUrl(player.imageId)" alt="" class="h-full w-full object-cover" />
                </div>
                <span class="truncate text-sm font-bold text-white">{{ player.username }}</span>
              </div>
              <div class="flex justify-center">
                <RoleBadge v-if="hasRevealedRole(player.role)" :role="player.role!" />
                <span v-else class="text-xs text-amber-100/50">—</span>
              </div>
              <span
                class="text-right text-xs font-extrabold uppercase"
                :class="player.isAlive ? 'text-green-400' : 'text-red-400'"
              >
                {{ player.isAlive ? t('voting.alive') : t('voting.eliminated') }}
              </span>
            </div>
          </section>

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

.roster-head,
.roster-row {
  display: grid;
  grid-template-columns: 1.5fr 0.9fr 0.7fr;
  align-items: center;
  gap: 0.5rem;
}

.roster-head {
  padding: 0 0.35rem 0.35rem;
  font-size: 0.65rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgb(255 226 170 / 0.7);
}

.roster-row {
  margin-bottom: 0.4rem;
  padding: 0.55rem 0.65rem;
  border-radius: 0.85rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
}

.roster-row--dead {
  border-color: rgb(208 87 79 / 0.75);
  box-shadow: 0 0 10px rgb(208 87 79 / 0.2);
}

.roster-avatar {
  width: 2rem;
  height: 2rem;
  flex-shrink: 0;
  overflow: hidden;
  border-radius: 0.5rem;
  border: 2px solid rgb(180 130 70 / 0.8);
  background: rgb(var(--color-game-input-rgb));
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
