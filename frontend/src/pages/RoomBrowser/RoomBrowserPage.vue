<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import BottomBar from '@/components/layout/BottomBar/BottomBar.vue'
import Button from '@/components/common/Button/Button.vue'
import Input from '@/components/common/Input/Input.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import Spinner from '@/components/common/Spinner/Spinner.vue'
import { ArrowLeftIcon, ArrowPathIcon, UserGroupIcon } from '@heroicons/vue/24/outline'
import { useGameStore } from '@/stores/game.store'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { gameService } from '@/services/game.service'
import { useLocaleFormat } from '@/composables/useGameLabels'
import { resolveActiveMatchRoute } from '@/utils/roomFlow'
import { images } from '@/assets/images'
import type { ActiveMatchResponse, OpenRoomDto } from '@/types/api'

const router = useRouter()
const game = useGameStore()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()

const joining = ref(false)
const joiningRoomId = ref<string | null>(null)
const leaving = ref(false)
const refreshing = ref(false)
const joinCode = ref('')
const openRooms = ref<OpenRoomDto[]>([])
const activeMatch = ref<ActiveMatchResponse | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null

const queueCount = computed(() => game.queueCount)
const inQueue = computed(() => game.isInQueue)
const hasActiveMatch = computed(
  () => activeMatch.value?.hasActiveMatch === true && !!activeMatch.value?.matchId,
)

onMounted(async () => {
  await refresh()
  pollTimer = setInterval(() => {
    void refreshQuiet()
  }, 4000)
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
})

async function refreshQuiet() {
  try {
    await game.refreshQueueStatus()
    openRooms.value = await gameService.listOpenRooms()
    activeMatch.value = await gameService.getActiveMatch()
  } catch {
    // Keep last known values on transient network errors.
  }
}

async function refresh() {
  refreshing.value = true
  try {
    await refreshQuiet()
  } finally {
    refreshing.value = false
  }
}

async function enterLobby(matchId: string, sessionToken: string) {
  auth.setMatchSession(matchId, sessionToken)
  void auth.loadProfile()
  await router.push({ name: 'lobby', params: { id: matchId } })
}

async function joinRoom(room: OpenRoomDto) {
  joiningRoomId.value = room.matchId
  try {
    const res = await gameService.joinOpenRoom({ matchId: room.matchId })
    await enterLobby(res.matchId, res.sessionToken)
  } catch (e: unknown) {
    settings.reportError(e, t('rooms.joinFailed'))
  } finally {
    joiningRoomId.value = null
  }
}

async function joinByCode() {
  const raw = joinCode.value.trim()
  if (!raw) {
    settings.pushToast('error', t('rooms.codeRequired'))
    return
  }

  joining.value = true
  try {
    const res = await gameService.joinOpenRoom({ roomCode: raw })
    await enterLobby(res.matchId, res.sessionToken)
  } catch (e: unknown) {
    settings.reportError(e, t('rooms.joinFailed'))
  } finally {
    joining.value = false
  }
}

async function findMatch() {
  joining.value = true
  try {
    const res = await game.joinQueue()
    if (res.matchId && res.sessionToken) {
      await enterLobby(res.matchId, res.sessionToken)
      return
    }
    settings.pushToast('info', res.message || t('toast.searchingMatch'))
  } catch (e: unknown) {
    settings.reportError(e, t('toast.queueJoinFailed'))
  } finally {
    joining.value = false
  }
}

async function cancelSearch() {
  leaving.value = true
  try {
    await gameService.leaveQueue()
    await game.refreshQueueStatus()
  } catch (e: unknown) {
    settings.reportError(e)
  } finally {
    leaving.value = false
  }
}

async function resumeMatch() {
  const match = activeMatch.value
  if (!match?.matchId) return
  if (match.sessionToken) auth.setMatchSession(match.matchId, match.sessionToken)
  await router.replace({
    name: resolveActiveMatchRoute(match),
    params: { id: match.matchId },
  })
}
</script>

<template>
  <MobileFrame fullscreen>
    <PageBackdrop :src="images.backgrounds.login" :overlay-opacity="0.55" class="flex min-h-screen flex-col">
      <div class="relative z-10 flex min-h-screen flex-col">
        <WoodPanel
          as="header"
          texture="wood02"
          class="join-header-bar mx-3 mt-[calc(0.5rem+var(--safe-top))] flex items-center justify-between px-3 py-2"
        >
          <IconButton :icon="ArrowLeftIcon" :label="t('common.back')" @click="router.push('/home')" />
          <IconButton
            :icon="ArrowPathIcon"
            :label="t('rooms.refresh')"
            :disabled="refreshing"
            @click="refresh"
          />
        </WoodPanel>

        <main class="flex flex-1 flex-col px-4 pb-6 pt-3">
          <h1 class="join-title text-center font-display text-3xl uppercase tracking-widest">
            {{ t('home.joinRoom') }}
          </h1>
          <p class="mt-1 text-center text-sm text-white/85">{{ t('rooms.subtitle') }}</p>

          <WoodPanel
            class="join-panel mx-auto mt-4 w-full max-w-sm space-y-4 p-4"
          >
            <section v-if="hasActiveMatch" class="join-resume px-3 py-3 text-left">
              <p class="text-sm font-bold text-white">{{ t('rooms.activeMatch') }}</p>
              <p class="mt-0.5 text-xs text-white/75">
                {{ activeMatch?.roomName || t('common.room') }} ·
                {{ t('common.day', { n: activeMatch?.day ?? 1 }) }} ·
                {{ t('common.survivorCount', { n: activeMatch?.playersAlive ?? 0 }) }}
              </p>
              <Button
                block
                size="sm"
                variant="secondary"
                class="mt-3 uppercase tracking-wide"
                @click="resumeMatch"
              >
                {{ t('rooms.returnToMatch') }}
              </Button>
            </section>

            <section class="space-y-2 text-left">
              <p class="text-xs uppercase tracking-wide text-white/70">{{ t('rooms.joinByCode') }}</p>
              <div class="flex gap-2">
                <Input
                  v-model="joinCode"
                  class="flex-1"
                  :placeholder="t('rooms.codePlaceholder')"
                  :maxlength="36"
                  autocomplete="off"
                  @keyup.enter="joinByCode"
                />
                <Button
                  size="md"
                  variant="success"
                  class="shrink-0 uppercase tracking-wide"
                  :loading="joining && !joiningRoomId"
                  @click="joinByCode"
                >
                  {{ t('rooms.joinCode') }}
                </Button>
              </div>
            </section>

            <section class="space-y-2 text-left">
              <div class="flex items-center justify-between">
                <p class="text-xs uppercase tracking-wide text-white/70">{{ t('rooms.openRooms') }}</p>
                <Spinner v-if="refreshing" size="sm" />
              </div>

              <div v-if="openRooms.length === 0" class="join-status px-3 py-4 text-center">
                <p class="text-sm font-semibold text-white/90">{{ t('rooms.empty') }}</p>
                <p class="mt-1 text-xs text-white/65">{{ t('rooms.emptyHint') }}</p>
              </div>

              <ul v-else class="max-h-56 space-y-2 overflow-y-auto">
                <li
                  v-for="room in openRooms"
                  :key="room.matchId"
                  class="join-room-row flex items-center gap-2 px-3 py-2.5"
                >
                  <div class="min-w-0 flex-1">
                    <p class="truncate text-sm font-bold text-white">{{ room.roomName }}</p>
                    <p class="mt-0.5 text-xs text-white/70">
                      {{ t('rooms.playersCount', { current: room.playerCount, max: room.maxPlayers }) }}
                      · {{ t('rooms.code', { code: room.roomCode }) }}
                    </p>
                    <p class="text-[11px] text-white/55">
                      {{
                        room.hostUsername
                          ? t('rooms.host', { name: room.hostUsername })
                          : room.fillWithBots
                            ? t('rooms.botsOn')
                            : t('rooms.botsOff')
                      }}
                    </p>
                  </div>
                  <Button
                    size="sm"
                    variant="success"
                    class="shrink-0 uppercase tracking-wide"
                    :loading="joiningRoomId === room.matchId"
                    :disabled="!!joiningRoomId && joiningRoomId !== room.matchId"
                    @click="joinRoom(room)"
                  >
                    {{ t('rooms.join') }}
                  </Button>
                </li>
              </ul>
            </section>

            <section class="join-status flex items-center gap-3 px-3 py-3">
              <img
                :src="images.ui.matchmaking"
                alt=""
                class="h-12 w-12 shrink-0 rounded-xl object-cover"
                aria-hidden="true"
              />
              <div class="min-w-0 flex-1 text-left">
                <p class="text-xs uppercase tracking-wide text-white/70">
                  {{ t('rooms.findMatchSection') }}
                </p>
                <p class="text-xs text-white/75">
                  {{ t('common.playersInQueue') }}:
                  <span class="font-bold text-game-accent">{{ formatLocaleNumber(queueCount) }}</span>
                </p>
                <p class="mt-0.5 text-xs text-white/65">
                  {{ inQueue ? t('lobby.searching') : t('rooms.notSearching') }}
                </p>
              </div>
              <Spinner v-if="inQueue" size="sm" />
              <UserGroupIcon v-else class="h-5 w-5 text-white/60" />
            </section>

            <Button
              v-if="!inQueue"
              block
              size="lg"
              variant="secondary"
              class="uppercase tracking-wide"
              :loading="joining && !joiningRoomId"
              @click="findMatch"
            >
              {{ t('lobby.findMatch') }}
            </Button>
            <Button
              v-else
              block
              size="lg"
              variant="danger"
              class="uppercase tracking-wide"
              :loading="leaving"
              @click="cancelSearch"
            >
              {{ t('rooms.cancelSearch') }}
            </Button>

            <Button
              block
              variant="secondary"
              class="uppercase tracking-wide"
              @click="router.push('/rooms/create')"
            >
              {{ t('lobby.createRoom') }}
            </Button>
          </WoodPanel>
        </main>

        <BottomBar wood-texture />
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.join-header-bar {
  border-radius: var(--radius-3xl);
  background-repeat: repeat-x;
  background-position: center;
  background-size: 220px 100%;
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  box-shadow:
    0 4px 0 rgb(0 0 0 / 0.35),
    inset 0 1px 0 rgb(255 255 255 / 0.12);
}

.join-title {
  color: rgb(var(--color-game-accent-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 0 6px rgb(var(--color-on-game-rgb)),
    0 3px 0 rgb(0 0 0 / 0.8),
    0 6px 14px rgb(0 0 0 / 0.45);
}

.join-panel {
  border-radius: 1.5rem;
  background-repeat: repeat;
  background-size: 180px auto;
  background-color: rgb(var(--color-game-input-rgb));
  border: 2px solid rgb(140 95 55 / 0.85);
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.4),
    0 0 22px rgb(192 132 252 / 0.25),
    inset 0 1px 0 rgb(255 255 255 / 0.1);
}

.join-status,
.join-resume,
.join-room-row {
  border-radius: 1rem;
  background: rgb(20 10 6 / 0.6);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
}
</style>
