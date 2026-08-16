<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore } from '@/stores/ui.store'
import { gameService } from '@/services/game.service'
import { clientService } from '@/services/client.service'
import { resolveActiveMatchRoute } from '@/utils/roomFlow'
import { APP_VERSION, isClientSupported } from '@/utils/appVersion'
import { preloadGameAssets } from '@/utils/preloadAssets'
import splashImage from '@/assets/images/splash.png'

const MIN_MS = 6000
const router = useRouter()
const auth = useAuthStore()
const ui = useUiStore()
const { t } = useI18n()

const progress = ref(0)
const currentStep = ref('')
const errorMessage = ref<string | null>(null)

onMounted(() => {
  ui.setSuppressBusy(true)
  void boot()
})

onUnmounted(() => {
  ui.setSuppressBusy(false)
})

async function boot() {
  errorMessage.value = null
  currentStep.value = t('splash.engine')
  const started = Date.now()
  const ticker = setInterval(() => {
    const elapsed = Date.now() - started
    progress.value = Math.min(92, Math.round((elapsed / MIN_MS) * 92))
  }, 100)

  try {
    const status = await clientService.boot()
    if (!status.engineHealthy) throw new Error(t('splash.engineDown'))

    currentStep.value = t('splash.database')
    if (!status.databaseHealthy) throw new Error(t('splash.databaseDown'))

    currentStep.value = t('splash.version')
    if (!isClientSupported(APP_VERSION, status.minClientVersion)) {
      throw new Error(t('splash.updateRequired'))
    }

    currentStep.value = t('splash.assets')
    await preloadGameAssets()

    currentStep.value = t('splash.session')
    await auth.initGuest()

    currentStep.value = t('splash.match')
    const active = await gameService.getActiveMatch()
    clearInterval(ticker)
    progress.value = 100
    currentStep.value = ''
    const wait = MIN_MS - (Date.now() - started)
    if (wait > 0) await sleep(wait)

    if (active.hasActiveMatch && active.matchId && active.sessionToken) {
      auth.setMatchSession(active.matchId, active.sessionToken)
      router.replace({
        name: resolveActiveMatchRoute(active),
        params: { id: active.matchId },
      })
      return
    }
    router.replace({ name: 'home' })
  } catch (e) {
    clearInterval(ticker)
    progress.value = 100
    errorMessage.value = e instanceof Error ? e.message : t('splash.engineDown')
  }
}

function sleep(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}
</script>

<template>
  <MobileFrame fullscreen>
    <div class="relative flex min-h-screen flex-col items-center justify-end pb-2xl">
      <img
        :src="splashImage"
        alt="Zombie vs Human"
        class="absolute inset-0 h-full w-full object-cover"
      />
      <div class="absolute inset-0 bg-bg/40" aria-hidden="true" />

      <div class="relative z-10 w-full max-w-xs px-lg pb-[calc(var(--safe-bottom)+var(--space-xl))]">
        <p v-if="errorMessage" class="mb-2 text-center text-sm font-semibold text-danger">
          {{ errorMessage }}
        </p>
        <button
          v-if="errorMessage"
          type="button"
          class="mb-3 w-full rounded-pill bg-on-game/15 py-2 text-sm font-bold text-on-game"
          @click="boot"
        >
          {{ t('common.retry') }}
        </button>

        <div class="h-2 overflow-hidden rounded-pill bg-overlay/40 ring-1 ring-border/10">
          <div
            class="h-full rounded-pill bg-secondary transition-all duration-normal ease-out"
            :style="{ width: `${progress}%` }"
          />
        </div>
        <p
          v-if="currentStep && !errorMessage"
          class="mt-2 text-center text-xs font-semibold text-on-game/85"
        >
          {{ currentStep }}
        </p>
      </div>
    </div>
  </MobileFrame>
</template>
