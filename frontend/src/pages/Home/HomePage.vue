<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import BottomBar from '@/components/layout/BottomBar/BottomBar.vue'
import Avatar from '@/components/common/Avatar/Avatar.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import { Cog6ToothIcon } from '@heroicons/vue/24/outline'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleFormat } from '@/composables/useGameLabels'
import { images } from '@/assets/images'

const router = useRouter()
const auth = useAuthStore()
const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()

onMounted(() => {
  void auth.loadProfile()
})
</script>

<template>
  <MobileFrame fullscreen>
    <PageBackdrop :src="images.backgrounds.login" :overlay-opacity="0.5" class="flex min-h-screen flex-col">
      <div class="relative z-10 flex min-h-screen flex-col">
        <!-- Hub top bar: avatar | coins | settings -->
        <header class="hub-topbar mx-3 mt-[calc(0.5rem+var(--safe-top))] flex items-center gap-3 px-3 py-2">
          <button type="button" class="shrink-0" @click="router.push('/profile')">
            <Avatar
              :name="auth.displayName ?? t('common.survivor')"
              :image-url="auth.avatarUrl"
              size="sm"
              ring="success"
            />
          </button>

          <div class="hub-coins mx-auto flex items-center gap-1.5 px-4 py-1.5">
            <img :src="images.ui.coin" alt="" class="h-5 w-5" aria-hidden="true" />
            <span class="text-sm font-bold text-white">{{ formatLocaleNumber(auth.coinCount) }}</span>
          </div>

          <IconButton
            :icon="Cog6ToothIcon"
            :label="t('common.settings')"
            @click="router.push('/settings')"
          />
        </header>

        <main class="flex flex-1 flex-col px-4 pb-4 pt-6">
          <p class="mb-2 text-center text-sm text-white/85">
            {{ t('home.welcomeName', { name: auth.displayName ?? t('common.survivor') }) }}
          </p>
          <h1 class="hub-title mb-8 text-center font-display text-4xl uppercase tracking-widest">
            {{ t('home.lobbyTitle') }}
          </h1>

          <div class="mx-auto w-full max-w-sm space-y-4">
            <button
              type="button"
              class="hub-action hub-action--create"
              @click="router.push('/rooms/create')"
            >
              <img
                :src="images.ui.quickMatch"
                alt=""
                class="h-14 w-14 shrink-0 rounded-xl object-cover"
                aria-hidden="true"
              />
              <span class="min-w-0 flex-1 text-left">
                <span class="block text-lg font-bold uppercase tracking-wide text-white">
                  {{ t('home.createRoom') }}
                </span>
                <span class="mt-0.5 block text-xs text-white/75">
                  {{ t('home.createRoomDesc') }}
                </span>
              </span>
            </button>

            <button
              type="button"
              class="hub-action hub-action--join"
              @click="router.push('/rooms')"
            >
              <img
                :src="images.ui.matchmaking"
                alt=""
                class="h-14 w-14 shrink-0 rounded-xl object-cover"
                aria-hidden="true"
              />
              <span class="min-w-0 flex-1 text-left">
                <span class="block text-lg font-bold uppercase tracking-wide text-white">
                  {{ t('home.joinRoom') }}
                </span>
                <span class="mt-0.5 block text-xs text-white/75">
                  {{ t('home.joinRoomDesc') }}
                </span>
              </span>
            </button>
          </div>
        </main>

        <BottomBar />
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style scoped>
.hub-topbar {
  border-radius: var(--radius-3xl);
  background: linear-gradient(180deg, rgb(var(--color-game-wood-start-rgb)) 0%, rgb(var(--color-game-wood-end-rgb)) 100%);
  box-shadow:
    0 4px 0 rgb(0 0 0 / 0.35),
    inset 0 1px 0 rgb(255 255 255 / 0.12);
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
}

.hub-coins {
  border-radius: var(--radius-pill);
  background: rgb(20 10 6 / 0.65);
  border: 1px solid rgb(180 130 70 / 0.35);
}

.hub-title {
  color: rgb(var(--color-game-accent-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 0 6px rgb(var(--color-on-game-rgb)),
    0 3px 0 rgb(0 0 0 / 0.8),
    0 6px 14px rgb(0 0 0 / 0.45);
}

.hub-action {
  display: flex;
  width: 100%;
  align-items: center;
  gap: 0.9rem;
  border-radius: 1.25rem;
  padding: 1rem 1.1rem;
  background: linear-gradient(180deg, rgb(var(--color-game-wood-start-rgb)) 0%, rgb(var(--color-game-wood-end-rgb)) 100%);
  border: 2px solid rgb(140 95 55 / 0.8);
  box-shadow:
    0 6px 0 rgb(0 0 0 / 0.4),
    inset 0 1px 0 rgb(255 255 255 / 0.1);
  transition: transform 0.15s ease, filter 0.15s ease;
}

.hub-action:active {
  transform: scale(0.98);
}

.hub-action--create {
  box-shadow:
    0 6px 0 rgb(0 0 0 / 0.4),
    0 0 18px rgb(74 222 128 / 0.45),
    inset 0 1px 0 rgb(255 255 255 / 0.1);
}

.hub-action--join {
  box-shadow:
    0 6px 0 rgb(0 0 0 / 0.4),
    0 0 18px rgb(192 132 252 / 0.45),
    inset 0 1px 0 rgb(255 255 255 / 0.1);
}
</style>
