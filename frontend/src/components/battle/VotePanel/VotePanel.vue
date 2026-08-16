<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import PlayerGrid from '@/components/lobby/PlayerGrid/PlayerGrid.vue'
import Button from '@/components/common/Button/Button.vue'
import type { RoomPlayerDto } from '@/types/api'

defineProps<{
  players: RoomPlayerDto[]
  selectedId?: string | null
  disabled?: boolean
}>()

defineEmits<{ vote: [userId: string] }>()

const { t } = useI18n()
</script>

<template>
  <div class="space-y-3">
    <PlayerGrid
      :players="players"
      :selected-id="selectedId"
      @select="$emit('vote', $event)"
    />
    <Button
      block
      :disabled="!selectedId || disabled"
      @click="selectedId && $emit('vote', selectedId)"
    >
      {{ t('game.castVote') }}
    </Button>
  </div>
</template>
