<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import BottomBar from '@/components/layout/BottomBar/BottomBar.vue'
import ToggleSwitch from '@/components/common/ToggleSwitch/ToggleSwitch.vue'
import Button from '@/components/common/Button/Button.vue'
import Input from '@/components/common/Input/Input.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import { ArrowLeftIcon, QuestionMarkCircleIcon } from '@heroicons/vue/24/outline'
import { useAuthStore } from '@/stores/auth.store'
import { useSettingsStore } from '@/stores/settings.store'
import { gameService } from '@/services/game.service'
import { useLocaleFormat } from '@/composables/useGameLabels'
import { images } from '@/assets/images'
import type { RoomConfigResponse } from '@/types/api'

const router = useRouter()
const auth = useAuthStore()
const settings = useSettingsStore()
const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()

const config = ref<RoomConfigResponse | null>(null)
const roomName = ref('Survivor Squad')
const maxPlayers = ref(8)
const fillWithBots = ref(true)
const creating = ref(false)
const loadError = ref<string | null>(null)

const coinBalance = computed(() => auth.coinCount)
const balanceKnown = computed(() => !!auth.profile || !!auth.guestProfile)

const entryFee = computed(() => config.value?.entryFeeCoins ?? 5)
const minPlayers = computed(() => config.value?.minPlayers ?? 4)
const maxPlayersLimit = computed(() => config.value?.maxPlayers ?? 12)
const botTimeout = computed(() => config.value?.botFillTimeoutSeconds ?? 10)

onMounted(async () => {
  // Without the profile the balance reads as zero and would block room creation.
  await auth.loadProfile().catch(() => undefined)
  try {
    config.value = await gameService.getRoomConfig()
    maxPlayers.value = config.value.defaultMaxPlayers
    fillWithBots.value = config.value.fillWithBotsDefault
  } catch (e: unknown) {
    const msg = t('createRoom.loadFailed')
    loadError.value = msg
    settings.reportError(e, msg)
  }
})

function decreasePlayers() {
  maxPlayers.value = Math.max(minPlayers.value, maxPlayers.value - 1)
}

function increasePlayers() {
  maxPlayers.value = Math.min(maxPlayersLimit.value, maxPlayers.value + 1)
}

async function createAndEnter() {
  if (!roomName.value.trim()) {
    settings.pushToast('error', t('createRoom.nameRequired'))
    return
  }
  if (balanceKnown.value && coinBalance.value < entryFee.value) {
    settings.pushToast('error', t('createRoom.notEnoughCoins'))
    return
  }

  creating.value = true
  try {
    const res = await gameService.createRoom({
      roomName: roomName.value.trim(),
      maxPlayers: maxPlayers.value,
      fillWithBots: fillWithBots.value,
    })
    auth.setMatchSession(res.matchId, res.sessionToken)
    void auth.loadProfile()
    router.replace({ name: 'lobby', params: { id: res.matchId } })
  } catch (e: unknown) {
    settings.reportError(e, t('createRoom.createFailed'))
  } finally {
    creating.value = false
  }
}
</script>

<template>
  <MobileFrame fullscreen>
    <PageBackdrop :src="images.backgrounds.login" :overlay-opacity="0.55" class="flex min-h-screen flex-col">
      <div class="relative z-10 flex min-h-screen flex-col">
        <header class="flex items-center justify-between px-4 pt-[calc(0.75rem+var(--safe-top))]">
          <IconButton :icon="ArrowLeftIcon" :label="t('common.back')" @click="router.back()" />
          <IconButton
            :icon="QuestionMarkCircleIcon"
            :label="t('createRoom.help')"
            @click="settings.pushToast('info', t('createRoom.helpHint'))"
          />
        </header>

        <main class="flex flex-1 flex-col px-4 pb-4">
          <h1 class="create-title mt-2 text-center font-display text-3xl uppercase tracking-widest">
            {{ t('createRoom.title') }}
          </h1>
          <p class="mt-1 text-center text-sm text-white/85">{{ t('createRoom.subtitle') }}</p>

          <div class="create-panel mx-auto mt-5 w-full max-w-sm flex-1 space-y-5 p-4">
            <section class="space-y-2 text-left">
              <label class="text-sm font-semibold text-white">{{ t('createRoom.roomName') }}</label>
              <div
                class="create-field"
                :style="{ backgroundImage: `url(${images.ui.inputFrameSmall})` }"
              >
                <Input
                  v-model="roomName"
                  :placeholder="t('createRoom.roomNamePlaceholder')"
                  :aria-label="t('createRoom.roomName')"
                  :maxlength="40"
                />
              </div>
            </section>

            <section class="space-y-2 text-left">
              <label class="text-sm font-semibold text-white">{{ t('createRoom.maxPlayers') }}</label>
              <div class="flex items-center justify-center gap-3">
                <button type="button" class="create-stepper" @click="decreasePlayers">−</button>
                <div class="create-counter">{{ maxPlayers }}</div>
                <button type="button" class="create-stepper" @click="increasePlayers">+</button>
              </div>
              <p class="text-center text-xs text-white/70">
                {{ t('createRoom.playersRange', { min: minPlayers, max: maxPlayersLimit }) }}
              </p>
            </section>

            <section class="flex items-center justify-between gap-3">
              <div class="min-w-0 text-left">
                <p class="text-sm font-semibold text-white">{{ t('createRoom.autoFillBots') }}</p>
                <p class="text-xs text-white/70">
                  {{ t('createRoom.botsJoinAfter', { n: botTimeout }) }}
                </p>
              </div>
              <ToggleSwitch
                v-model="fillWithBots"
                :ariaLabel="t('createRoom.autoFillBots')"
                size="md"
              />
            </section>

            <section class="text-left">
              <p class="text-sm font-semibold text-white">{{ t('createRoom.entryFee') }}</p>
              <p class="mt-1 flex items-center gap-1.5 text-base font-bold text-secondary">
                <img :src="images.ui.coin" alt="" class="h-5 w-5" aria-hidden="true" />
                {{ t('common.coins', { n: formatLocaleNumber(entryFee) }) }}
              </p>
              <p class="mt-1 text-xs text-white/70">
                {{ t('createRoom.youHave', { n: formatLocaleNumber(coinBalance) }) }}
              </p>
            </section>

            <p v-if="loadError" class="text-center text-sm text-danger">{{ loadError }}</p>

            <Button
              block
              size="lg"
              variant="success"
              class="create-submit uppercase tracking-wide"
              :loading="creating"
              @click="createAndEnter"
            >
              {{ t('createRoom.createAndEnter') }}
            </Button>
            <p class="text-center text-xs text-white/65">{{ t('createRoom.codeHint') }}</p>
          </div>
        </main>

        <BottomBar />
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.create-title {
  color: rgb(var(--color-game-accent-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 0 6px rgb(var(--color-on-game-rgb)),
    0 3px 0 rgb(0 0 0 / 0.8),
    0 6px 14px rgb(0 0 0 / 0.45);
}

.create-panel {
  border-radius: 1.5rem;
  background: linear-gradient(180deg, rgb(var(--color-game-wood-start-rgb)) 0%, rgb(var(--color-game-wood-end-rgb)) 100%);
  border: 2px solid rgb(140 95 55 / 0.85);
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.4),
    0 0 22px rgb(74 222 128 / 0.25),
    0 0 22px rgb(192 132 252 / 0.2),
    inset 0 1px 0 rgb(255 255 255 / 0.1);
}

.create-field {
  height: 3.25rem;
  display: flex;
  align-items: center;
  background-repeat: no-repeat;
  background-position: center;
  background-size: 100% 100%;
  padding: 0 0.7rem;
}

.create-field :deep(.ds-input),
.create-field :deep(input) {
  height: 2.25rem;
  width: 100%;
  border: none !important;
  background: rgb(var(--color-game-input-rgb)) !important;
  box-shadow: none !important;
  text-align: left;
  color: rgb(var(--color-on-game-rgb));
  border-radius: 9999px;
  padding-inline: 1rem;
}

.create-stepper {
  width: 2.75rem;
  height: 2.75rem;
  border-radius: 0.75rem;
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-wood-start-rgb)) 0%,
    rgb(var(--color-game-wood-end-rgb)) 100%
  );
  border: 2px solid rgb(160 110 60 / 0.8);
  color: white;
  font-size: 1.5rem;
  font-weight: 700;
  line-height: 1;
}

.create-counter {
  min-width: 4.5rem;
  height: 2.75rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 0.75rem;
  background: rgb(20 10 6 / 0.75);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  color: white;
  font-size: 1.25rem;
  font-weight: 700;
}

.create-submit {
  border-width: 3px !important;
  border-color: rgb(220 255 180 / 0.85) !important;
  border-radius: var(--radius-pill) !important;
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.35),
    0 12px 24px rgb(34 197 94 / 0.35) !important;
}
</style>
