<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import BottomBar from '@/components/layout/BottomBar/BottomBar.vue'
import ToggleSwitch from '@/components/common/ToggleSwitch/ToggleSwitch.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import LanguageSwitcher from '@/components/common/LanguageSwitcher/LanguageSwitcher.vue'
import Input from '@/components/common/Input/Input.vue'
import {
  ArrowLeftIcon,
  ArrowPathRoundedSquareIcon,
  ArrowRightOnRectangleIcon,
  GlobeAltIcon,
  MusicalNoteIcon,
  SpeakerWaveIcon,
} from '@heroicons/vue/24/outline'
import { useSettingsStore } from '@/stores/settings.store'
import { useAuthStore } from '@/stores/auth.store'
import { images } from '@/assets/images'

const settings = useSettingsStore()
const auth = useAuthStore()
const router = useRouter()
const { t } = useI18n()

const recoverUsername = ref('')
const recoverPassword = ref('')
const recoverCode = ref('')
const otpSent = ref(false)
const recoverMessage = ref('')

async function logout() {
  await auth.logout()
  router.replace('/login')
}

async function sendRecoverCode() {
  recoverMessage.value = ''
  try {
    await auth.sendRecoverAccountOtp(recoverUsername.value, recoverPassword.value)
    otpSent.value = true
    recoverMessage.value = t('settings.recoverSent')
  } catch {
    // auth.error already set
  }
}

async function confirmRecover() {
  recoverMessage.value = ''
  try {
    await auth.verifyRecoverAccountOtp(recoverUsername.value, recoverPassword.value, recoverCode.value)
    recoverUsername.value = ''
    recoverPassword.value = ''
    recoverCode.value = ''
    otpSent.value = false
    recoverMessage.value = t('settings.recoverSuccess')
  } catch {
    // auth.error already set
  }
}
</script>

<template>
  <MobileFrame fullscreen>
    <PageBackdrop :src="images.backgrounds.login" :overlay-opacity="0.55" class="flex min-h-screen flex-col">
      <div class="relative z-10 flex min-h-screen flex-col">
        <header class="flex items-center px-4 pt-[calc(0.75rem+var(--safe-top))]">
          <IconButton :icon="ArrowLeftIcon" :label="t('common.back')" @click="router.back()" />
        </header>

        <main class="flex flex-1 flex-col overflow-y-auto px-4 pb-4 pt-2">
          <h1 class="settings-title text-center font-display text-3xl uppercase tracking-widest">
            {{ t('settings.title') }}
          </h1>

          <div class="settings-panel mx-auto mt-5 w-full max-w-sm space-y-1 p-3">
            <div class="settings-row">
              <div class="settings-row__label">
                <GlobeAltIcon class="h-5 w-5 text-secondary" />
                <span>{{ t('settings.language') }}</span>
              </div>
              <LanguageSwitcher />
            </div>

            <div class="settings-row">
              <div class="settings-row__label">
                <SpeakerWaveIcon class="h-5 w-5 text-secondary" />
                <span>{{ t('settings.sound') }}</span>
              </div>
              <ToggleSwitch
                v-model="settings.soundEnabled"
                :ariaLabel="t('settings.sound')"
                size="sm"
              />
            </div>

            <div class="settings-row">
              <div class="settings-row__label">
                <MusicalNoteIcon class="h-5 w-5 text-secondary" />
                <span>{{ t('settings.music') }}</span>
              </div>
              <ToggleSwitch
                v-model="settings.musicEnabled"
                :ariaLabel="t('settings.music')"
                size="sm"
              />
            </div>

            <div class="settings-row">
              <div class="settings-row__label">
                <ArrowPathRoundedSquareIcon class="h-5 w-5 text-secondary" />
                <span>{{ t('settings.reducedMotion') }}</span>
              </div>
              <ToggleSwitch
                v-model="settings.reducedMotion"
                :ariaLabel="t('settings.reducedMotion')"
                size="sm"
              />
            </div>

          </div>

          <section v-if="auth.isGuest" class="settings-panel mx-auto mt-5 w-full max-w-sm space-y-3 p-3">
            <h2 class="text-center text-sm font-extrabold uppercase tracking-widest text-white">
              {{ t('settings.recoverTitle') }}
            </h2>
            <p class="text-center text-xs leading-5 text-white/75">
              {{ t('settings.recoverHint') }}
            </p>
            <Input
              v-model="recoverUsername"
              :placeholder="t('settings.recoverUsername')"
              :aria-label="t('settings.recoverUsername')"
              autocomplete="username"
            />
            <Input
              v-model="recoverPassword"
              type="password"
              :placeholder="t('settings.recoverPassword')"
              :aria-label="t('settings.recoverPassword')"
              autocomplete="current-password"
            />
            <button
              type="button"
              class="settings-secondary"
              :disabled="auth.isLoading || !recoverUsername || !recoverPassword"
              @click="sendRecoverCode"
            >
              {{ t('settings.sendCode') }}
            </button>
            <template v-if="otpSent">
              <Input
                v-model="recoverCode"
                :placeholder="t('settings.recoverOtp')"
                :aria-label="t('settings.recoverOtp')"
                autocomplete="one-time-code"
                :maxlength="6"
              />
              <button
                type="button"
                class="settings-secondary"
                :disabled="auth.isLoading || !recoverCode"
                @click="confirmRecover"
              >
                {{ t('settings.confirmRecover') }}
              </button>
            </template>
            <p v-if="recoverMessage" class="text-center text-xs text-white/90">{{ recoverMessage }}</p>
            <p v-if="auth.error" class="text-center text-xs text-red-300" role="alert">{{ auth.error }}</p>
          </section>

          <button type="button" class="settings-logout mx-auto mt-5 w-full max-w-sm" @click="logout">
            <ArrowRightOnRectangleIcon class="h-5 w-5" />
            {{ t('common.logout') }}
          </button>
        </main>

        <BottomBar />
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.settings-title {
  color: rgb(var(--color-game-accent-rgb));
  text-shadow: 0 0 2px rgb(var(--color-on-game-rgb)), 0 3px 0 rgb(0 0 0 / 0.8);
}

.settings-panel {
  border-radius: 1.25rem;
  background: linear-gradient(180deg, rgb(70 42 24 / 0.92) 0%, rgb(35 20 12 / 0.95) 100%);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.75);
}

.settings-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  min-height: 3.25rem;
  padding: 0.5rem 0.35rem;
  border-bottom: 1px solid rgb(255 255 255 / 0.08);
}

.settings-row:last-child {
  border-bottom: 0;
}

.settings-row__label {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  font-weight: 600;
  color: rgb(var(--color-on-game-rgb));
}

.settings-secondary {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  min-height: 2.75rem;
  border-radius: var(--radius-pill);
  font-size: 0.85rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgb(var(--color-on-game-rgb));
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border: 2px solid rgb(180 255 190 / 0.35);
}

.settings-secondary:disabled {
  opacity: 0.45;
}

.settings-logout {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  min-height: 3.25rem;
  font-size: 0.95rem;
  border-radius: var(--radius-pill);
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  background: linear-gradient(
    180deg,
    rgb(var(--color-danger-rgb)) 0%,
    rgb(var(--color-primary-rgb)) 100%
  );
  color: rgb(var(--color-on-game-rgb));
  border: 3px solid rgb(255 180 180 / 0.5);
  box-shadow:
    0 6px 0 rgb(0 0 0 / 0.35),
    0 10px 20px rgb(224 75 75 / 0.25);
}
</style>
