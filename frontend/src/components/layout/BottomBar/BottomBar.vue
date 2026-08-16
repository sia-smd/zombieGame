<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import {
  HomeIcon,
  QueueListIcon,
  UserIcon,
} from '@heroicons/vue/24/solid'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()

const items = computed(() => [
  { to: '/home', icon: HomeIcon, label: t('nav.lobby') },
  { to: '/rooms', icon: QueueListIcon, label: t('nav.rooms') },
  { to: '/profile', icon: UserIcon, label: t('nav.profile') },
])

function isActive(to: string) {
  if (to === '/home') return route.path === '/home'
  return route.path.startsWith(to)
}
</script>

<template>
  <WoodPanel
    as="nav"
    texture="wood02"
    class="hub-bottombar flex items-center justify-around px-2 pb-[calc(0.5rem+var(--safe-bottom))] pt-2"
  >
    <button
      v-for="item in items"
      :key="item.to"
      type="button"
      class="flex min-w-[4.5rem] flex-col items-center gap-1 rounded-2xl px-3 py-2 text-[11px] font-semibold transition"
      :class="isActive(item.to) ? 'hub-nav-active' : 'text-white/70'"
      @click="router.push(item.to)"
    >
      <component :is="item.icon" class="h-6 w-6" />
      {{ item.label }}
    </button>
  </WoodPanel>
</template>

<style scoped>
.hub-bottombar {
  background-color: rgb(var(--color-game-panel-rgb));
  background-repeat: repeat-x;
  background-position: center;
  background-size: 220px 100%;
  border-top: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  box-shadow: inset 0 1px 0 rgb(255 255 255 / 0.1);
}

.hub-nav-active {
  color: rgb(var(--color-game-cta-border-rgb));
  text-shadow: 0 0 10px rgb(124 252 0 / 0.55);
}
</style>
