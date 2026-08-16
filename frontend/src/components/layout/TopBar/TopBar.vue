<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import Avatar from '@/components/common/Avatar/Avatar.vue'
import LanguageSwitcher from '@/components/common/LanguageSwitcher/LanguageSwitcher.vue'
import { Cog6ToothIcon, ArrowLeftIcon } from '@heroicons/vue/24/outline'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleFormat } from '@/composables/useGameLabels'
import { images } from '@/assets/images'

defineProps<{
  title?: string
  showBack?: boolean
}>()

const router = useRouter()
const auth = useAuthStore()
const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()
</script>

<template>
  <header class="flex items-center gap-2 px-4 pb-3 pt-[calc(0.75rem+var(--safe-top))]">
    <IconButton
      v-if="showBack"
      :icon="ArrowLeftIcon"
      :label="t('common.back')"
      @click="router.back()"
    />
    <div class="min-w-0 flex-1">
      <h1 v-if="title" class="truncate font-display text-lg text-gold">{{ title }}</h1>
      <slot name="subtitle" />
    </div>
    <LanguageSwitcher />
    <div class="flex items-center gap-1 rounded-full bg-overlay/40 px-2 py-0.5">
      <img :src="images.ui.coin" alt="" class="h-4 w-4" aria-hidden="true" />
      <span class="text-xs font-bold text-white">{{ formatLocaleNumber(auth.coinCount) }}</span>
    </div>
    <button type="button" class="shrink-0" @click="router.push('/profile')">
      <Avatar
        :name="auth.displayName ?? t('common.survivor')"
        :image-url="auth.avatarUrl"
        size="sm"
        ring="gold"
      />
    </button>
    <IconButton :icon="Cog6ToothIcon" :label="t('common.settings')" @click="router.push('/settings')" />
  </header>
</template>
