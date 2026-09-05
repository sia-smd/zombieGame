<script setup lang="ts">
import { onMounted, onUnmounted, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import Button from '@/components/common/Button/Button.vue'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore } from '@/stores/ui.store'
import { gameService } from '@/services/game.service'
import { clientService } from '@/services/client.service'
import { resolveActiveMatchRoute } from '@/utils/roomFlow'
import { APP_VERSION, isClientSupported } from '@/utils/appVersion'
import { preloadGameAssets } from '@/utils/preloadAssets'
import { authSession } from '@/services/api'
import splashImage from '@/assets/images/splash.png'

type SplashPhase = 'loading' | 'ready' | 'entering'

const router = useRouter()
const auth = useAuthStore()
const ui = useUiStore()
const { t } = useI18n()

const phase = ref<SplashPhase>('loading')
const progress = ref(0)
const currentStep = ref('')
const errorMessage = ref<string | null>(null)
const entering = ref(false)

const progressPct = computed(() => `${progress.value}%`)

onMounted(() => {
  ui.setSuppressBusy(true)
  void boot()
})

onUnmounted(() => {
  ui.setSuppressBusy(false)
})

async function boot() {
  errorMessage.value = null
  phase.value = 'loading'
  progress.value = 0

  try {
    // Step 1: health check
    currentStep.value = t('splash.engine')
    progress.value = 5
    const status = await clientService.boot()
    if (!status.engineHealthy) throw new Error(t('splash.engineDown'))

    progress.value = 15
    currentStep.value = t('splash.database')
    if (!status.databaseHealthy) throw new Error(t('splash.databaseDown'))

    // Step 2: version check
    progress.value = 25
    currentStep.value = t('splash.version')
    if (!isClientSupported(APP_VERSION, status.minClientVersion)) {
      throw new Error(t('splash.updateRequired'))
    }

    // Step 3: preload critical assets with real progress (25→90)
    currentStep.value = t('splash.assets')
    await preloadGameAssets((p) => {
      progress.value = 25 + Math.round((p.loaded / Math.max(p.total, 1)) * 65)
    })

    progress.value = 95
    currentStep.value = ''
    progress.value = 100
    phase.value = 'ready'
  } catch (e) {
    progress.value = 100
    errorMessage.value = e instanceof Error ? e.message : t('splash.engineDown')
  }
}

async function enterGame() {
  if (entering.value) return
  entering.value = true
  phase.value = 'entering'
  currentStep.value = t('splash.session')
  errorMessage.value = null

  try {
    // If already signed in, resume; otherwise create guest
    if (authSession.isSignedIn()) {
      auth.hydrateFromStorage()
      if (!auth.profile) await auth.loadProfile().catch(() => undefined)
    } else {
      await auth.initGuest()
    }

    currentStep.value = t('splash.match')
    const active = await gameService.getActiveMatch()

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
    entering.value = false
    phase.value = 'ready'
    errorMessage.value = e instanceof Error ? e.message : t('splash.sessionFailed')
  }
}

function goLogin() {
  router.push({ name: 'login' })
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
        <!-- Error state -->
        <p v-if="errorMessage" class="mb-2 text-center text-sm font-semibold text-danger">
          {{ errorMessage }}
        </p>
        <button
          v-if="errorMessage && phase === 'loading'"
          type="button"
          class="mb-3 w-full rounded-pill bg-on-game/15 py-2 text-sm font-bold text-on-game"
          @click="boot"
        >
          {{ t('common.retry') }}
        </button>
        <button
          v-if="errorMessage && phase === 'ready'"
          type="button"
          class="mb-3 w-full rounded-pill bg-on-game/15 py-2 text-sm font-bold text-on-game"
          @click="enterGame"
        >
          {{ t('common.retry') }}
        </button>

        <!-- Loading phase: progress bar -->
        <template v-if="phase === 'loading'">
          <div class="h-2 overflow-hidden rounded-pill bg-overlay/40 ring-1 ring-border/10">
            <div
              class="h-full rounded-pill bg-secondary transition-all duration-normal ease-out"
              :style="{ width: progressPct }"
            />
          </div>
          <p
            v-if="currentStep && !errorMessage"
            class="mt-2 text-center text-xs font-semibold text-on-game/85"
          >
            {{ currentStep }}
          </p>
        </template>

        <!-- Ready phase: Start button + Log in link -->
        <template v-if="phase === 'ready' && !errorMessage">
          <Button
            block
            size="lg"
            variant="success"
            class="splash-start-btn uppercase tracking-wide"
            @click="enterGame"
          >
            {{ t('splash.start') }}
          </Button>
          <button
            type="button"
            class="mt-md block w-full text-center text-xs text-on-game/70 underline underline-offset-2 transition-colors hover:text-on-game"
            @click="goLogin"
          >
            {{ t('splash.loginLink') }}
          </button>
        </template>

        <!-- Entering phase: spinner -->
        <template v-if="phase === 'entering'">
          <p class="text-center text-sm font-semibold text-on-game/85 animate-pulse">
            {{ currentStep }}
          </p>
        </template>
      </div>
    </div>
  </MobileFrame>
</template>

<style scoped>
.splash-start-btn {
  border-width: 3px !important;
  border-color: rgb(220 255 180 / 0.85) !important;
  border-radius: var(--radius-pill) !important;
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.35),
    0 12px 24px rgb(34 197 94 / 0.35) !important;
}
</style>
