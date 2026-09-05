<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import Button from '@/components/common/Button/Button.vue'
import Input from '@/components/common/Input/Input.vue'
import PasswordInput from '@/components/common/Input/PasswordInput.vue'
import OtpInput from '@/components/common/Input/OtpInput.vue'
import { useAuthStore } from '@/stores/auth.store'
import { authSession } from '@/services/api'
import { images } from '@/assets/images'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const { t } = useI18n()

const nickname = ref('')
const accountId = ref('')
const password = ref('')
const otpCode = ref('')
const accountModeOpen = ref(false)
const otpSent = ref(false)
const otpMessage = ref('')

const canSendOtp = computed(() => accountId.value.trim().length > 0 && password.value.trim().length > 0)
const canVerifyOtp = computed(() => otpCode.value.trim().length === 6)

onMounted(async () => {
  if (authSession.isSignedIn()) {
    auth.hydrateFromStorage()
    if (!auth.profile) await auth.loadProfile().catch(() => undefined)
    if (auth.profile) {
      const redirect = (route.query.redirect as string) || '/home'
      router.replace(redirect)
      return
    }
  }
})

async function playAsGuest() {
  await auth.initGuest(nickname.value)
  await redirectAfterAuth()
}

function toggleAccountMode() {
  accountModeOpen.value = !accountModeOpen.value
  auth.error = null
}

async function sendOtp() {
  otpMessage.value = ''
  try {
    const res = await auth.sendRecoverAccountOtp(accountId.value.trim(), password.value)
    otpSent.value = true
    otpCode.value = ''
    otpMessage.value = res.message || t('login.otpSent')
  } catch {
    // auth.error is shown by the store
  }
}

async function verifyOtp() {
  otpMessage.value = ''
  try {
    await auth.verifyRecoverAccountOtp(accountId.value.trim(), password.value, otpCode.value.trim())
    await redirectAfterAuth()
  } catch {
    // auth.error is shown by the store
  }
}

async function redirectAfterAuth() {
  const redirect = (route.query.redirect as string) || '/home'
  await router.replace(redirect)
}
</script>

<template>
  <MobileFrame fullscreen>
    <PageBackdrop
      :src="images.backgrounds.login"
      :overlay-opacity="0.55"
      class="flex min-h-screen flex-col px-lg pb-2xl pt-[calc(var(--safe-top)+4rem)]"
    >
      <div class="relative z-10 flex flex-1 flex-col items-center text-center">
        <img
          :src="images.brand.logoSmall"
          :alt="t('app.title')"
          class="mb-2xl h-20 w-auto object-contain drop-shadow-lg"
        />

        <h1 class="login-hero-title font-display text-[2rem] leading-tight tracking-wide text-text-primary">
          {{ t('login.title') }}
        </h1>
        <p class="mt-md text-body text-text-primary/90 drop-shadow">
          {{ t('login.subtitle') }}
        </p>

        <div class="mt-2xl w-full max-w-sm space-y-lg">
          <div
            class="login-nickname-frame"
            :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }"
          >
            <Input
              v-model="nickname"
              :placeholder="t('login.nickname')"
              :aria-label="t('login.nickname')"
              autocomplete="nickname"
              :maxlength="24"
            />
          </div>

          <p v-if="auth.error" class="text-center text-body text-danger" role="alert">{{ auth.error }}</p>

          <Button
            block
            size="lg"
            variant="success"
            class="login-guest-btn uppercase tracking-wide"
            :loading="auth.isLoading"
            @click="playAsGuest"
          >
            {{ t('login.playAsGuest') }}
          </Button>

          <button type="button" class="login-account-toggle" @click="toggleAccountMode">
            {{ t('login.accountToggle') }}
          </button>

          <div v-if="accountModeOpen" class="login-account-stack">
            <div
              class="login-field-frame"
              :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }"
            >
              <Input
                v-model="accountId"
                :placeholder="t('login.accountId')"
                :aria-label="t('login.accountId')"
                autocomplete="username"
              />
            </div>
            <div
              class="login-field-frame"
              :style="{ backgroundImage: `url(${images.ui.inputFrameBig})` }"
            >
              <PasswordInput
                v-model="password"
                :placeholder="t('login.password')"
                :aria-label="t('login.password')"
                autocomplete="current-password"
              />
            </div>
            <Button
              block
              size="lg"
              variant="success"
              class="login-account-btn uppercase tracking-wide"
              :loading="auth.isLoading && !otpSent"
              :disabled="!canSendOtp"
              @click="sendOtp"
            >
              {{ otpSent ? t('login.resendOtp') : t('login.sendOtp') }}
            </Button>

            <template v-if="otpSent">
              <div class="login-otp-card">
                <p class="login-otp-card__hint">{{ t('login.codePrompt') }}</p>
                <OtpInput v-model="otpCode" :disabled="auth.isLoading" />
              </div>
              <Button
                block
                size="lg"
                variant="success"
                class="login-account-btn uppercase tracking-wide"
                :loading="auth.isLoading"
                :disabled="!canVerifyOtp"
                @click="verifyOtp"
              >
                {{ t('login.verifyOtp') }}
              </Button>
            </template>

            <p v-if="otpMessage" class="text-center text-caption text-secondary">{{ otpMessage }}</p>
          </div>
        </div>

        <p v-if="auth.error" class="mt-md text-center text-body text-danger" role="alert">{{ auth.error }}</p>

        <p class="mt-lg text-caption text-text-primary/80">
          {{ t('login.deviceSavedLocally') }}
        </p>

        <p class="mt-auto pt-2xl text-caption text-text-secondary underline underline-offset-2">
          {{ t('login.termsPrivacy') }}
        </p>
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.login-hero-title {
  text-shadow:
    0 2px 0 rgb(0 0 0 / 0.85),
    0 4px 10px rgb(0 0 0 / 0.45);
}

.login-nickname-frame {
  width: 100%;
  height: 4.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  background-repeat: no-repeat;
  background-position: center;
  background-size: 100% 100%;
  padding: 0 0.7rem;
}

.login-field-frame {
  width: 100%;
  min-height: 3.6rem;
  display: flex;
  align-items: center;
  justify-content: center;
  background-repeat: no-repeat;
  background-position: center;
  background-size: 100% 100%;
  padding: 0 0.65rem;
}

.login-nickname-frame :deep(.ds-input),
.login-nickname-frame :deep(input),
.login-field-frame :deep(.ds-input),
.login-field-frame :deep(input) {
  height: 3.1rem;
  width: 100%;
  border: none !important;
  background: rgb(var(--color-game-input-rgb)) !important;
  box-shadow: none !important;
  text-align: center;
  color: rgb(var(--color-on-game-rgb));
  font-size: 1rem;
  font-weight: 600;
  border-radius: 9999px;
  padding-inline: 1rem;
}

.login-nickname-frame :deep(input::placeholder),
.login-field-frame :deep(input::placeholder) {
  color: rgb(255 255 255 / 0.55);
}

.login-account-stack {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.login-account-toggle {
  align-self: center;
  color: rgb(255 255 255 / 0.82);
  font-size: 0.82rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  text-shadow: 0 2px 8px rgb(0 0 0 / 0.35);
}

.login-otp-card {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 0.9rem 0.85rem;
  border-radius: 1.1rem;
  background: linear-gradient(180deg, rgb(28 23 20 / 0.72) 0%, rgb(17 14 12 / 0.78) 100%);
  border: 1px solid rgb(255 255 255 / 0.08);
}

.login-otp-card__hint {
  font-size: 0.8rem;
  line-height: 1.4;
  color: rgb(255 255 255 / 0.78);
}

.login-account-btn {
  border-width: 3px !important;
  border-color: rgb(220 255 180 / 0.85) !important;
  border-radius: var(--radius-pill) !important;
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.35),
    0 12px 24px rgb(34 197 94 / 0.35) !important;
}

.login-guest-btn {
  border-width: 3px !important;
  border-color: rgb(220 255 180 / 0.85) !important;
  border-radius: var(--radius-pill) !important;
  box-shadow:
    0 8px 0 rgb(0 0 0 / 0.35),
    0 12px 24px rgb(34 197 94 / 0.35) !important;
}
</style>
