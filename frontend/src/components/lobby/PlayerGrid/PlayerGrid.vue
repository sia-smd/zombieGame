<script setup lang="ts">
import PlayerCard from '@/components/lobby/PlayerCard/PlayerCard.vue'
import type { RoomPlayerDto } from '@/types/api'

defineProps<{
  players: RoomPlayerDto[]
  hostId?: string
  readyIds?: string[]
  selectedId?: string | null
}>()

defineEmits<{ select: [id: string] }>()
</script>

<template>
  <div class="grid grid-cols-2 gap-3 sm:grid-cols-3">
    <PlayerCard
      v-for="player in players"
      :key="player.userId"
      :player="player"
      :is-host="hostId === player.userId"
      :is-ready="readyIds?.includes(player.userId)"
      :selected="selectedId === player.userId"
      @select="$emit('select', $event)"
    />
  </div>
</template>
