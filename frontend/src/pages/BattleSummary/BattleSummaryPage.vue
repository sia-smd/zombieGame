<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import RoomMoodGauge from '@/components/room/RoomMoodGauge/RoomMoodGauge.vue'
import { useRoomStore } from '@/stores/room.store'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useRoomSession } from '@/composables/useRoomSession'
import { useCountdown } from '@/composables/useAnimation'
import { RoomPhase, BattlePublicAction } from '@/types/enums'
import { sameUserId } from '@/utils/ids'
import { images } from '@/assets/images'
import type { BattleSummaryDto } from '@/types/api'

const props = defineProps<{ id: string }>()
const room = useRoomStore()
const { t } = useI18n()

const { ready, isBootstrapping, bootstrap } = useRoomSession(() => props.id, 'resume')
useMatchPhaseNavigation(() => props.id, ready)
const isDaySummary = computed(() => room.currentPhase === RoomPhase.DaySummary)
const summaries = computed(() => room.battleSummaries)
const daySummary = computed(() => room.daySummary)

const { clock } = useCountdown(() => room.state?.phaseEndsAt)

const nextPhaseLabel = computed(() => t('phases.room.discussion'))

const eliminatedIds = computed(() => daySummary.value?.eliminatedPlayerIds ?? [])
const infectedIds = computed(() => daySummary.value?.newlyInfectedPlayerIds ?? [])
const restingIds = computed(() => daySummary.value?.restingPlayerIds ?? [])
const aliveCount = computed(
  () => daySummary.value?.aliveCount ?? room.players.filter((p) => p.isAlive).length,
)
const deadCount = computed(() => room.players.filter((p) => !p.isAlive).length)

function actionLabel(action: BattlePublicAction) {
  if (action === BattlePublicAction.Action) return t('battleSummary.action')
  if (action === BattlePublicAction.Pass) return t('battleSummary.pass')
  return t('battleSummary.none')
}

function actionClass(action: BattlePublicAction) {
  if (action === BattlePublicAction.Action) return 'badge--action'
  if (action === BattlePublicAction.Pass) return 'badge--pass'
  return 'badge--none'
}

function wasEliminated(userId: string) {
  return eliminatedIds.value.some((id) => sameUserId(id, userId))
}

function battleOutcome(summary: BattleSummaryDto) {
  const dead = [summary.player1Id, summary.player2Id].filter(wasEliminated)
  if (dead.length === 0) return null
  const names = dead.map((id) =>
    sameUserId(id, summary.player1Id) ? summary.player1Name : summary.player2Name,
  )
  return t('battleSummary.eliminated', { name: names.join(', ') })
}

onMounted(async () => {
  await bootstrap()
})

</script>

<template>
  <MobileFrame fullscreen>
    <LoadingOverlay :visible="isBootstrapping || room.isConnecting" :message="t('game.syncing')" />

    <PageBackdrop
      :src="images.backgrounds.login"
      :overlay-opacity="0.7"
      class="flex h-screen flex-col overflow-hidden"
    >
      <div class="relative z-10 flex h-full min-h-0 flex-col px-3 pb-3 pt-[calc(0.5rem+var(--safe-top))]">
        <WoodPanel class="summary-board flex min-h-0 flex-1 flex-col overflow-hidden">
          <header class="px-4 pt-4 text-center">
            <h1 class="summary-title font-display text-2xl uppercase tracking-widest">
              {{ t('battleSummary.title', { n: room.dayNumber }) }}
            </h1>
            <p class="mt-0.5 text-xs font-semibold uppercase tracking-wide text-amber-100/75">
              {{ isDaySummary ? t('battleSummary.daySummary') : t('battleSummary.public') }}
            </p>
            <div class="summary-timer mt-3">
              {{ t('battleSummary.nextIn') }}: {{ clock }}
            </div>
          </header>

          <div class="mt-3 min-h-0 flex-1 space-y-3 overflow-y-auto overscroll-contain px-3 pb-5">
            <section
              v-for="(battle, index) in summaries"
              :key="`${battle.player1Id}-${battle.player2Id}-${index}`"
              class="battle-card"
            >
              <p class="battle-card__title">{{ t('battleSummary.battle', { n: index + 1 }) }}</p>
              <div class="flex items-center justify-between gap-2">
                <div class="fighter">
                  <div class="fighter-avatar fighter-avatar--a">
                    <img :src="images.avatars.default" alt="" class="h-full w-full object-cover" />
                  </div>
                  <p class="fighter-name">{{ battle.player1Name }}</p>
                  <span class="badge" :class="actionClass(battle.player1Action)">
                    {{ actionLabel(battle.player1Action) }}
                  </span>
                </div>

                <span class="vs">{{ t('battleSummary.vs') }}</span>

                <div class="fighter">
                  <div class="fighter-avatar fighter-avatar--b">
                    <img :src="images.avatars.default" alt="" class="h-full w-full object-cover" />
                  </div>
                  <p class="fighter-name">{{ battle.player2Name }}</p>
                  <span class="badge" :class="actionClass(battle.player2Action)">
                    {{ actionLabel(battle.player2Action) }}
                  </span>
                </div>
              </div>
              <p v-if="battleOutcome(battle)" class="elim-line">{{ battleOutcome(battle) }}</p>
            </section>

            <p v-if="!summaries.length" class="py-6 text-center text-sm text-amber-100/70">
              {{ t('battleSummary.empty') }}
            </p>

            <section v-if="isDaySummary || daySummary" class="day-stats">
              <div class="stat">
                <span>{{ t('battleSummary.eliminatedCount') }}</span>
                <strong class="stat--bad">{{ eliminatedIds.length }}</strong>
              </div>
              <div class="stat">
                <span>{{ t('battleSummary.infectedCount') }}</span>
                <strong class="stat--warn">{{ infectedIds.length }}</strong>
              </div>
              <div class="stat">
                <span>{{ t('battleSummary.restingCount') }}</span>
                <strong>{{ restingIds.length }}</strong>
              </div>
              <div class="stat">
                <span>{{ t('battleSummary.aliveCount') }}</span>
                <strong class="stat--ok">{{ aliveCount }}</strong>
              </div>
              <div class="stat">
                <span>{{ t('battleSummary.deadTotal') }}</span>
                <strong class="stat--bad">{{ deadCount }}</strong>
              </div>
            </section>

            <div v-if="isDaySummary || daySummary" class="mood-wrap">
              <p class="mood-hint">{{ t('battleSummary.nextDayHint') }}</p>
              <RoomMoodGauge :mood="room.roomMood" />
            </div>
          </div>

          <div class="px-3 pb-3 pt-1">
            <div class="next-btn">
              {{ t('battleSummary.nextPhase', { phase: nextPhaseLabel }) }}
            </div>
          </div>
        </WoodPanel>
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.summary-board {
  border-radius: 1.5rem;
  background-color: rgb(var(--color-game-panel-rgb));
  background-repeat: repeat;
  background-size: 200px auto;
  border: 3px solid rgb(150 100 55 / 0.9);
  box-shadow:
    inset 0 2px 0 rgb(255 255 255 / 0.12),
    0 10px 26px rgb(0 0 0 / 0.5);
}

.summary-title {
  color: rgb(var(--color-game-title-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 2px 0 rgb(0 0 0 / 0.7),
    0 5px 12px rgb(0 0 0 / 0.5);
}

.summary-timer {
  display: inline-block;
  min-width: 10rem;
  padding: 0.4rem 1rem;
  border-radius: 0.7rem;
  background: rgb(15 8 4 / 0.85);
  border: 2px solid rgb(180 130 70 / 0.7);
  color: rgb(var(--color-game-title-rgb));
  font-size: 0.85rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.battle-card {
  border-radius: 1rem;
  padding: 0.75rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
}

.battle-card__title {
  margin-bottom: 0.6rem;
  font-size: 0.8rem;
  font-weight: 800;
  color: rgb(var(--color-on-game-rgb));
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.fighter {
  display: flex;
  width: 5.5rem;
  flex-direction: column;
  align-items: center;
  gap: 0.3rem;
}

.fighter-avatar {
  width: 3.5rem;
  height: 3.5rem;
  border-radius: 0.75rem;
  overflow: hidden;
  border: 3px solid;
  background: rgb(var(--color-game-input-rgb));
}

.fighter-avatar--a {
  border-color: rgb(var(--color-game-cta-end-rgb));
}

.fighter-avatar--b {
  border-color: rgb(var(--color-danger-rgb));
}

.fighter-name {
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.7rem;
  font-weight: 700;
  color: rgb(var(--color-on-game-rgb));
}

.vs {
  font-size: 0.95rem;
  font-weight: 900;
  color: rgb(var(--color-game-title-rgb));
}

.badge {
  min-width: 4.5rem;
  padding: 0.2rem 0.55rem;
  border-radius: 9999px;
  font-size: 0.65rem;
  font-weight: 800;
  text-align: center;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  background: rgb(15 8 4 / 0.8);
  border: 1px solid rgb(var(--color-game-border-rgb) / 0.7);
}

.badge--action {
  color: rgb(var(--color-game-cta-border-rgb));
}

.badge--pass {
  color: rgb(var(--color-text-secondary-rgb));
}

.badge--none {
  color: rgb(var(--color-on-game-rgb) / 0.5);
}

.elim-line {
  margin-top: 0.65rem;
  text-align: center;
  font-size: 0.75rem;
  font-weight: 800;
  color: rgb(var(--color-room-mood-critical-rgb));
}

.day-stats {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.5rem;
  border-radius: 1rem;
  padding: 0.75rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
}

.mood-wrap {
  border-radius: 1rem;
  padding: 0.75rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
}

.mood-hint {
  margin-bottom: 0.5rem;
  font-size: 0.7rem;
  color: rgb(255 226 170 / 0.8);
}

.stat {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.4rem;
  font-size: 0.7rem;
  color: rgb(255 226 170 / 0.85);
}

.stat strong {
  font-size: 1rem;
  color: rgb(var(--color-on-game-rgb));
}

.stat--bad {
  color: rgb(var(--color-room-mood-critical-rgb)) !important;
}

.stat--warn {
  color: rgb(var(--color-game-accent-rgb)) !important;
}

.stat--ok {
  color: rgb(var(--color-game-cta-border-rgb)) !important;
}

.next-btn {
  display: flex;
  min-height: 3.1rem;
  align-items: center;
  justify-content: center;
  border-radius: var(--radius-pill);
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border: 3px solid rgb(220 255 180 / 0.85);
  box-shadow: 0 6px 0 rgb(0 0 0 / 0.35);
  color: rgb(var(--color-on-game-rgb));
  font-size: 0.95rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  opacity: 0.85;
}
</style>
