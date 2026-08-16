<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Card from '@/components/common/Card/Card.vue'
import Badge from '@/components/common/Badge/Badge.vue'
import Timer from '@/components/common/Timer/Timer.vue'

defineProps<{
  roomId: string
  playerCount: number
  maxPlayers?: number
  phaseLabel?: string
  endsAt?: string | null
}>()

const { t } = useI18n()
</script>

<template>
  <Card variant="game" padding="md" glow="secondary" hoverable>
    <div class="flex items-center justify-between gap-md">
      <div>
        <p class="text-caption text-text-muted">{{ t('common.room') }}</p>
        <p class="font-mono text-subtitle font-bold text-secondary">{{ roomId.slice(0, 8).toUpperCase() }}</p>
      </div>
      <Timer v-if="endsAt" :ends-at="endsAt" />
    </div>
    <div class="mt-sm flex flex-wrap items-center gap-sm">
      <Badge
        v-if="phaseLabel"
        :label="phaseLabel"
        variant="gold"
      />
      <Badge
        :label="t('common.survivorCount', { n: playerCount })"
        variant="default"
      />
    </div>
  </Card>
</template>
