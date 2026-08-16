<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import { CheckIcon } from '@heroicons/vue/24/solid'
import { useRoomStore } from '@/stores/room.store'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useRoomSession } from '@/composables/useRoomSession'
import { useCountdown } from '@/composables/useAnimation'
import { roomService } from '@/services/room.service'
import { RoomPhase } from '@/types/enums'
import { sameUserId } from '@/utils/ids'
import { images } from '@/assets/images'
import { readStoredVote, writeStoredVote } from '@/utils/voteSession'
import type { RoomPlayerDto } from '@/types/api'

const props = defineProps<{ id: string }>()
const room = useRoomStore()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()

const ABSTAIN_ID = '00000000-0000-0000-0000-000000000000'
const voting = ref(false)
const hasVoted = ref(false)
const selected = ref<string | null>(null)

const { ready, sessionToken: token, isBootstrapping, bootstrap } = useRoomSession(
  () => props.id,
  'resume',
)
useMatchPhaseNavigation(() => props.id, ready)
const myId = computed(() => auth.resolvedUserId)
const iAmAlive = computed(
  () => room.players.find((p) => sameUserId(p.userId, myId.value))?.isAlive !== false,
)
const isResults = computed(() => room.currentPhase === RoomPhase.VoteResult)
const isVoting = computed(() => room.currentPhase === RoomPhase.Voting)

const alivePlayers = computed(() =>
  [...room.players]
    .filter((p) => p.isAlive)
    .sort((a, b) => a.seatIndex - b.seatIndex),
)

const voteCounts = computed(() => room.state?.voteCounts ?? {})
const eliminatedId = computed(() => room.state?.lastEliminatedPlayerId ?? null)

function voteCountFor(playerId: string) {
  const key = Object.keys(voteCounts.value).find((k) => sameUserId(k, playerId))
  return key ? voteCounts.value[key] : 0
}

/** During VoteResult, show anyone who received votes or was eliminated. */
const resultPlayers = computed(() => {
  const elim = eliminatedId.value
  const counts = voteCounts.value
  return [...room.players]
    .filter((p) => {
      if (p.isAlive) return true
      if (sameUserId(p.userId, elim)) return true
      return Object.keys(counts).some((k) => sameUserId(k, p.userId) && counts[k] > 0)
    })
    .sort((a, b) => voteCountFor(b.userId) - voteCountFor(a.userId) || a.seatIndex - b.seatIndex)
})

const { clock } = useCountdown(
  () => room.state?.phaseEndsAt,
  true,
  () => room.state?.phaseSecondsRemaining,
  () => room.snapshotReceivedAt,
)

const candidates = computed(() =>
  alivePlayers.value.filter((p) => !sameUserId(p.userId, myId.value)),
)

function restoreVoteFromSources() {
  const fromMe = room.myBattle?.myVoteTargetId
  if (fromMe) {
    selected.value = fromMe
    hasVoted.value = true
    writeStoredVote(props.id, room.dayNumber, { selected: fromMe, hasVoted: true })
    return
  }
  const stored = readStoredVote(props.id, room.dayNumber)
  if (stored) {
    selected.value = stored.selected
    hasVoted.value = stored.hasVoted
  }
}

watch(
  () => [room.dayNumber, room.myBattle?.myVoteTargetId, room.currentPhase] as const,
  ([day], previous) => {
    if (previous && previous[0] !== day) {
      selected.value = null
      hasVoted.value = false
    }
    restoreVoteFromSources()
  },
)

function isEliminated(player: RoomPlayerDto) {
  return !player.isAlive || sameUserId(player.userId, eliminatedId.value)
}

function selectTarget(userId: string) {
  if (!isVoting.value || !iAmAlive.value || hasVoted.value || voting.value) return
  selected.value = userId
}

async function confirmVote() {
  if (!iAmAlive.value || !selected.value || !token.value || hasVoted.value || voting.value) return
  voting.value = true
  try {
    await roomService.vote(props.id, token.value, selected.value)
    hasVoted.value = true
    writeStoredVote(props.id, room.dayNumber, {
      selected: selected.value,
      hasVoted: true,
    })
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    voting.value = false
  }
}

onMounted(async () => {
  await bootstrap()
  restoreVoteFromSources()
})

</script>

<template>
  <MobileFrame fullscreen>
    <LoadingOverlay :visible="isBootstrapping || room.isConnecting" :message="t('game.syncing')" />

    <PageBackdrop
      :src="images.backgrounds.voting"
      :overlay-opacity="0.7"
      class="flex h-screen flex-col overflow-hidden"
    >
      <div class="relative z-10 flex h-full flex-col px-3 pb-3 pt-[calc(0.5rem+var(--safe-top))]">
        <WoodPanel class="vote-board flex flex-1 flex-col overflow-hidden">
          <header class="px-4 pt-4 text-center">
            <h1 class="vote-title font-display text-2xl uppercase tracking-widest">
              {{
                isResults
                  ? t('voting.resultsTitle')
                  : t('voting.phaseTitle', { n: room.dayNumber })
              }}
            </h1>
            <div class="vote-timer mt-3">
              {{ isResults ? t('voting.nextDayIn') : t('voting.timeLeft') }}: {{ clock }}
            </div>
          </header>

          <div v-if="isVoting && !iAmAlive" class="mt-8 px-4 text-center text-sm font-semibold text-amber-100/80">
            {{ t('voting.deadCannotVote') }}
          </div>

          <!-- Voting list -->
          <div v-else-if="isVoting" class="mt-3 flex-1 space-y-2 overflow-y-auto px-3 pb-2">
            <button
              v-for="player in candidates"
              :key="player.userId"
              type="button"
              class="vote-row"
              :class="{ 'vote-row--selected': sameUserId(selected, player.userId) }"
              :disabled="hasVoted || voting"
              @click="selectTarget(player.userId)"
            >
              <div class="vote-avatar">
                <img :src="images.avatars.default" alt="" class="h-full w-full object-cover" />
              </div>
              <span class="vote-name">{{ player.username }}</span>
              <span
                class="vote-check"
                :class="{ 'vote-check--on': sameUserId(selected, player.userId) }"
              >
                <CheckIcon v-if="sameUserId(selected, player.userId)" class="h-5 w-5" />
              </span>
            </button>

            <button
              type="button"
              class="vote-row"
              :class="{ 'vote-row--selected': sameUserId(selected, ABSTAIN_ID) }"
              :disabled="hasVoted || voting"
              @click="selectTarget(ABSTAIN_ID)"
            >
              <div class="vote-avatar vote-avatar--abstain">–</div>
              <span class="vote-name">{{ t('voting.abstain') }}</span>
              <span
                class="vote-check"
                :class="{ 'vote-check--on': sameUserId(selected, ABSTAIN_ID) }"
              >
                <CheckIcon v-if="sameUserId(selected, ABSTAIN_ID)" class="h-5 w-5" />
              </span>
            </button>

            <p v-if="!candidates.length" class="py-8 text-center text-sm text-amber-100/70">
              {{ t('voting.noTargets') }}
            </p>
          </div>

          <!-- Vote results -->
          <div v-else class="mt-3 flex-1 space-y-2 overflow-y-auto px-3 pb-2">
            <div class="results-head">
              <span>{{ t('voting.colPlayer') }}</span>
              <span>{{ t('voting.colVotes') }}</span>
              <span>{{ t('voting.colStatus') }}</span>
            </div>
            <div
              v-for="player in resultPlayers"
              :key="player.userId"
              class="results-row"
              :class="{ 'results-row--dead': isEliminated(player) }"
            >
              <div class="flex min-w-0 items-center gap-2">
                <div class="vote-avatar vote-avatar--sm">
                  <img :src="images.avatars.default" alt="" class="h-full w-full object-cover" />
                </div>
                <span class="truncate text-sm font-bold text-white">{{ player.username }}</span>
              </div>
              <span class="text-center text-sm font-extrabold text-amber-200">
                {{ voteCountFor(player.userId) }}
              </span>
              <span
                class="text-right text-xs font-extrabold uppercase"
                :class="isEliminated(player) ? 'text-red-400' : 'text-green-400'"
              >
                {{ isEliminated(player) ? t('voting.eliminated') : t('voting.alive') }}
              </span>
            </div>

            <p v-if="!eliminatedId" class="pt-2 text-center text-sm font-semibold text-amber-100/80">
              {{ t('voting.noMajority') }}
            </p>
          </div>

          <div class="px-3 pb-3 pt-1">
            <button
              v-if="isVoting && iAmAlive"
              type="button"
              class="confirm-btn"
              :disabled="!selected || hasVoted || voting"
              @click="confirmVote"
            >
              {{ hasVoted ? t('voting.voteCast') : t('voting.confirm') }}
            </button>
            <div v-else-if="isVoting && !iAmAlive" class="confirm-btn confirm-btn--idle">
              {{ t('voting.deadCannotVote') }}
            </div>
            <div v-else class="confirm-btn confirm-btn--idle">
              {{ t('voting.startingNextDay') }}
            </div>
          </div>
        </WoodPanel>
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.vote-board {
  border-radius: 1.5rem;
  background-color: rgb(var(--color-game-panel-rgb));
  background-repeat: repeat;
  background-size: 200px auto;
  border: 3px solid rgb(150 100 55 / 0.9);
  box-shadow:
    inset 0 2px 0 rgb(255 255 255 / 0.12),
    0 10px 26px rgb(0 0 0 / 0.5);
}

.vote-title {
  color: rgb(var(--color-game-title-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 2px 0 rgb(0 0 0 / 0.7),
    0 5px 12px rgb(0 0 0 / 0.5);
}

.vote-timer {
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

.vote-row {
  display: flex;
  width: 100%;
  align-items: center;
  gap: 0.75rem;
  padding: 0.55rem 0.75rem;
  border-radius: 0.9rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  text-align: left;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.vote-row:disabled {
  opacity: 0.7;
}

.vote-row--selected {
  border-color: rgb(var(--color-game-cta-border-rgb));
  box-shadow: 0 0 14px rgb(var(--color-game-cta-border-rgb) / 0.45);
}

.vote-avatar {
  width: 2.75rem;
  height: 2.75rem;
  flex-shrink: 0;
  overflow: hidden;
  border-radius: 0.65rem;
  border: 2px solid rgb(180 130 70 / 0.8);
  background: rgb(var(--color-game-input-rgb));
}

.vote-avatar--sm {
  width: 2.1rem;
  height: 2.1rem;
  border-radius: 0.5rem;
}

.vote-avatar--abstain {
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgb(255 226 170 / 0.85);
  font-size: 1.4rem;
  font-weight: 800;
}

.vote-name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.95rem;
  font-weight: 700;
  color: rgb(var(--color-on-game-rgb));
}

.vote-check {
  display: flex;
  width: 2rem;
  height: 2rem;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 9999px;
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.8);
  background: rgb(15 8 4 / 0.6);
  color: transparent;
}

.vote-check--on {
  border-color: rgb(var(--color-game-cta-border-rgb));
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  color: rgb(var(--color-on-game-rgb));
}

.results-head,
.results-row {
  display: grid;
  grid-template-columns: 1.4fr 0.5fr 0.7fr;
  align-items: center;
  gap: 0.5rem;
}

.results-head {
  padding: 0 0.5rem 0.35rem;
  font-size: 0.65rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgb(255 226 170 / 0.7);
}

.results-row {
  padding: 0.55rem 0.65rem;
  border-radius: 0.85rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
}

.results-row--dead {
  border-color: rgb(208 87 79 / 0.75);
  box-shadow: 0 0 10px rgb(208 87 79 / 0.25);
}

.confirm-btn {
  display: flex;
  width: 100%;
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
  font-size: 1rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.confirm-btn:disabled {
  opacity: 0.5;
}

.confirm-btn--idle {
  opacity: 0.85;
  pointer-events: none;
}
</style>
