<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import { ArrowRightIcon } from '@heroicons/vue/24/outline'
import { useRoomStore } from '@/stores/room.store'
import { useGameLabels } from '@/composables/useGameLabels'
import { useRoomSession } from '@/composables/useRoomSession'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useSettingsStore } from '@/stores/settings.store'
import { roomService } from '@/services/room.service'
import { PlayerRole } from '@/types/enums'
import { getRoleImage } from '@/utils/imageAssets'
import { images } from '@/assets/images'
import gsap from 'gsap'

const props = defineProps<{ id: string }>()

const room = useRoomStore()
const settings = useSettingsStore()
const { t } = useI18n()
const { roleLabel } = useGameLabels()
const revealed = ref(false)
const loadFailed = ref(false)
const cardRef = ref<HTMLElement | null>(null)
const { sessionToken, bootstrap } = useRoomSession(() => props.id, 'resume')
const allowPhaseNav = ref(false)
useMatchPhaseNavigation(() => props.id, allowPhaseNav)

/** Private role data comes from RoomHub (myBattle), not the legacy GameHub. */
const myRole = computed(() => room.myBattle?.role ?? PlayerRole.Unknown)
/** Role identity always uses roles/*.png — never inventory card art. */
const roleCardImage = computed(() => getRoleImage(myRole.value))

const isHuman = computed(() => myRole.value === PlayerRole.Human)
const isPowerZombie = computed(() => myRole.value === PlayerRole.PowerZombie)
const isZombie = computed(() => myRole.value === PlayerRole.Zombie)
const isInfected = computed(() => isZombie.value || isPowerZombie.value)

/** GameStarted broadcasts omit private `me`; sync until the assigned role card arrives. */
async function loadAssignedRole(token: string, timeoutMs: number) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    await roomService.syncRoom(props.id, token).catch(() => undefined)
    if (myRole.value !== PlayerRole.Unknown) return true
    await new Promise((resolve) => setTimeout(resolve, 150))
  }
  return myRole.value !== PlayerRole.Unknown
}

onMounted(async () => {
  if (!(await bootstrap()) || !sessionToken.value) {
    loadFailed.value = true
    revealed.value = true
    return
  }

  const ok = await loadAssignedRole(sessionToken.value, 5000)
  loadFailed.value = !ok
  revealed.value = true

  if (cardRef.value && ok && !settings.reducedMotion) {
    gsap.fromTo(
      cardRef.value,
      { rotateY: 180, opacity: 0, scale: 0.92 },
      { rotateY: 0, opacity: 1, scale: 1, duration: 0.85, ease: 'back.out(1.4)' },
    )
  }

  if (ok) {
    window.setTimeout(() => {
      allowPhaseNav.value = true
    }, 1800)
  }
})

function continueToGame() {
  allowPhaseNav.value = true
}
</script>

<template>
  <MobileFrame fullscreen>
    <PageBackdrop :src="images.backgrounds.login" :overlay-opacity="0.6" class="flex min-h-screen flex-col">
      <div class="relative z-10 flex min-h-screen flex-col items-center justify-center px-4 py-8">
        <h1 class="reveal-title text-center font-display text-3xl uppercase tracking-widest">
          {{ t('revealRole.title') }}
        </h1>

        <div
          ref="cardRef"
          class="reveal-panel mt-5 w-full max-w-xs p-4"
          :class="{
            'reveal-panel--human': revealed && isHuman,
            'reveal-panel--zombie': revealed && isInfected,
            'reveal-panel--power': revealed && isPowerZombie,
          }"
        >
          <div v-if="revealed && !loadFailed" class="space-y-4 text-center">
            <div
              class="reveal-card-frame"
              :class="{
                'reveal-card-frame--human': isHuman,
                'reveal-card-frame--zombie': isInfected,
                'reveal-card-frame--power': isPowerZombie,
              }"
            >
              <img :src="roleCardImage" :alt="roleLabel(myRole)" class="reveal-card-img w-full" />
            </div>

            <span
              class="reveal-role-badge"
              :class="{
                'reveal-role-badge--human': isHuman,
                'reveal-role-badge--zombie': isInfected,
                'reveal-role-badge--power': isPowerZombie,
              }"
            >
              {{ roleLabel(myRole) }}
            </span>

            <p class="text-sm leading-relaxed text-white/80">{{ t('revealRole.hint') }}</p>
          </div>

          <div v-else-if="revealed && loadFailed" class="space-y-4 text-center">
            <p class="text-sm leading-relaxed text-white/80">{{ t('revealRole.loadFailed') }}</p>
          </div>

          <div v-else class="space-y-4 text-center">
            <div class="reveal-card-frame reveal-card-frame--hidden">
              <img
                :src="images.cards.back"
                :alt="t('revealRole.revealing')"
                class="reveal-card-img w-full reveal-card-img--back"
              />
            </div>
            <p class="reveal-loading animate-pulse-glow font-semibold text-secondary">
              {{ t('revealRole.revealing') }}
            </p>
          </div>
        </div>

        <button
          v-if="revealed"
          type="button"
          class="reveal-continue mt-5 w-full max-w-xs"
          @click="continueToGame"
        >
          {{ t('common.continue') }}
          <ArrowRightIcon class="h-5 w-5" />
        </button>
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.reveal-title {
  color: rgb(var(--color-game-accent-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 3px 0 rgb(0 0 0 / 0.8),
    0 6px 14px rgb(0 0 0 / 0.45);
}

.reveal-panel {
  border-radius: 1.25rem;
  background: linear-gradient(180deg, rgb(70 42 24 / 0.94) 0%, rgb(35 20 12 / 0.96) 100%);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.75);
  box-shadow: 0 10px 28px rgb(0 0 0 / 0.45);
  transition: border-color 0.4s ease, box-shadow 0.4s ease;
}

.reveal-panel--human {
  border-color: rgb(126 214 58 / 0.65);
  box-shadow:
    0 10px 28px rgb(0 0 0 / 0.45),
    0 0 24px rgb(126 214 58 / 0.2);
}

.reveal-panel--zombie {
  border-color: rgb(224 75 75 / 0.65);
  box-shadow:
    0 10px 28px rgb(0 0 0 / 0.45),
    0 0 24px rgb(224 75 75 / 0.2);
}

.reveal-panel--power {
  border-color: rgb(180 60 220 / 0.7);
  box-shadow:
    0 10px 28px rgb(0 0 0 / 0.45),
    0 0 28px rgb(180 60 220 / 0.25);
}

.reveal-card-frame {
  padding: 0.35rem;
  border-radius: 1rem;
  background: rgb(20 10 6 / 0.55);
  border: 2px solid rgb(255 255 255 / 0.15);
}

.reveal-card-frame--hidden {
  animation: reveal-shake 1.8s ease-in-out infinite;
}

.reveal-card-frame--human {
  background: linear-gradient(180deg, rgb(126 214 58 / 0.25) 0%, rgb(61 158 31 / 0.15) 100%);
  border-color: rgb(155 232 74 / 0.55);
  box-shadow: 0 0 18px rgb(126 214 58 / 0.25);
}

.reveal-card-frame--zombie {
  background: linear-gradient(180deg, rgb(224 75 75 / 0.25) 0%, rgb(143 26 26 / 0.15) 100%);
  border-color: rgb(255 120 120 / 0.55);
  box-shadow: 0 0 18px rgb(224 75 75 / 0.25);
}

.reveal-card-frame--power {
  background: linear-gradient(180deg, rgb(180 60 220 / 0.3) 0%, rgb(90 20 120 / 0.18) 100%);
  border-color: rgb(210 140 255 / 0.6);
  box-shadow: 0 0 20px rgb(180 60 220 / 0.3);
}

.reveal-card-img {
  display: block;
  border-radius: 0.75rem;
}

.reveal-card-img--back {
  opacity: 0.95;
}

.reveal-role-badge {
  display: inline-block;
  border-radius: 9999px;
  padding: 0.4rem 1.1rem;
  font-size: 0.9rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  border: 2px solid rgb(255 255 255 / 0.25);
  box-shadow: 0 4px 0 rgb(0 0 0 / 0.3);
}

.reveal-role-badge--human {
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  color: rgb(var(--color-on-game-rgb));
}

.reveal-role-badge--zombie {
  background: linear-gradient(
    180deg,
    rgb(var(--color-room-mood-critical-rgb)) 0%,
    rgb(var(--color-danger-rgb)) 100%
  );
  color: rgb(var(--color-on-game-rgb));
}

.reveal-role-badge--power {
  background: linear-gradient(180deg, rgb(180 75 224) 0%, rgb(90 22 128) 100%);
  color: rgb(var(--color-on-game-rgb));
}

.reveal-loading {
  font-size: 0.95rem;
  letter-spacing: 0.04em;
}

.reveal-continue {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  min-height: 3.5rem;
  font-size: 1.05rem;
  border-radius: var(--radius-pill);
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  color: rgb(var(--color-on-game-rgb));
  border: 3px solid rgb(220 255 180 / 0.85);
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.35),
    0 12px 24px rgb(34 197 94 / 0.3);
}

@keyframes reveal-shake {
  0%,
  100% {
    transform: rotate(0deg);
  }
  25% {
    transform: rotate(-1.5deg);
  }
  75% {
    transform: rotate(1.5deg);
  }
}
</style>
