<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Card from '@/components/common/Card/Card.vue'
import Badge from '@/components/common/Badge/Badge.vue'
import Timer from '@/components/common/Timer/Timer.vue'
import { useGameLabels } from '@/composables/useGameLabels'
import type { RoomStateDto } from '@/types/api'

const props = defineProps<{ room: RoomStateDto | null }>()
const { t } = useI18n()
const { roomPhaseLabel } = useGameLabels()

const aliveCount = () => props.room?.players.filter((p) => p.isAlive).length ?? 0
</script>

<template>
  <Card v-if="room" padding="md" glow="gold" class="space-y-2">
    <div class="flex items-center justify-between">
      <h3 class="game-title text-base">{{ t('common.day', { n: room.dayNumber }) }}</h3>
      <Timer :ends-at="room.phaseEndsAt" />
    </div>
    <Badge :label="roomPhaseLabel(room.currentPhase)" variant="gold" />
    <p class="text-xs text-mist">{{ t('common.survivorCount', { n: aliveCount() }) }}</p>
  </Card>
</template>
