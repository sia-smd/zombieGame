<script setup lang="ts">
import TopBar from '@/components/layout/TopBar/TopBar.vue'
import BottomBar from '@/components/layout/BottomBar/BottomBar.vue'

defineProps<{
  title?: string
  showBack?: boolean
  showNav?: boolean
}>()
</script>

<template>
  <div class="flex min-h-full flex-col">
    <TopBar :title="title" :show-back="showBack">
      <template #subtitle><slot name="header" /></template>
    </TopBar>
    <main class="flex-1 overflow-y-auto px-4 pb-4">
      <slot />
    </main>
    <BottomBar v-if="showNav !== false" />
  </div>
</template>
