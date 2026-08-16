<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import Button from '@/components/common/Button/Button.vue'
import Input from '@/components/common/Input/Input.vue'
import { useAuthStore } from '@/stores/auth.store'
import { images } from '@/assets/images'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const { t } = useI18n()

const nickname = ref('')

async function playAsGuest() {
  await auth.initGuest(nickname.value)
  const redirect = (route.query.redirect as string) || '/home'
  router.replace(redirect)
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
        </div>

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

.login-nickname-frame :deep(.ds-input),
.login-nickname-frame :deep(input) {
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

.login-nickname-frame :deep(input::placeholder) {
  color: rgb(255 255 255 / 0.55);
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
