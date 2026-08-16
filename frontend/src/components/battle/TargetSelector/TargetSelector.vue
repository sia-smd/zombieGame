<script setup lang="ts">
import Avatar from '@/components/common/Avatar/Avatar.vue'
import type { RoomPlayerDto } from '@/types/api'

defineProps<{
  players: RoomPlayerDto[]
  selectedId?: string | null
}>()

defineEmits<{ select: [userId: string] }>()
</script>

<template>
  <div class="flex flex-wrap justify-center gap-3">
    <button
      v-for="player in players"
      :key="player.userId"
      type="button"
      class="rounded-2xl p-1 transition ring-offset-2 ring-offset-void"
      :class="selectedId === player.userId ? 'ring-2 ring-gold' : ''"
      @click="$emit('select', player.userId)"
    >
      <Avatar
        :name="player.username"
        :alive="player.isAlive"
        :ring="selectedId === player.userId ? 'gold' : 'none'"
      />
    </button>
  </div>
</template>
