<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Card from '@/components/common/Card/Card.vue'

defineProps<{
  entries: Array<{ id: string; text: string; time?: string }>
}>()

const { t } = useI18n()
</script>

<template>
  <Card padding="sm" class="max-h-40 space-y-2 overflow-y-auto">
    <p
      v-for="entry in entries"
      :key="entry.id"
      class="text-xs text-mist"
    >
      <span v-if="entry.time" class="text-gold/70">{{ entry.time }} </span>
      {{ entry.text }}
    </p>
    <p v-if="!entries.length" class="text-center text-xs text-mist/70">{{ t('game.battleLogEmpty') }}</p>
  </Card>
</template>
