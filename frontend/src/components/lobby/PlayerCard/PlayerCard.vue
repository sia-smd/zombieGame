<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import Avatar from '@/components/common/Avatar/Avatar.vue'
import Badge from '@/components/common/Badge/Badge.vue'
import type { RoomPlayerDto } from '@/types/api'

const props = defineProps<{
  player: RoomPlayerDto
  isHost?: boolean
  isReady?: boolean
  selected?: boolean
}>()

defineEmits<{ select: [id: string] }>()

const { t } = useI18n()

const ring = computed(() => {
  if (!props.player.isAlive) return 'none' as const
  if (props.selected) return 'gold' as const
  return 'none' as const
})
</script>

<template>
  <button
    type="button"
    class="game-panel flex w-full flex-col items-center gap-2 p-3 text-center transition-transform active:scale-[0.98]"
    :class="selected ? 'ring-2 ring-gold/50' : ''"
    @click="$emit('select', player.userId)"
  >
    <Avatar :name="player.username" :alive="player.isAlive" :ring="ring" size="lg" />
    <p class="truncate text-sm font-semibold text-fog">{{ player.username }}</p>
    <div class="flex flex-wrap justify-center gap-1">
      <Badge v-if="isHost" :label="t('common.host')" variant="gold" />
      <Badge v-if="isReady" :label="t('common.ready')" variant="human" />
      <Badge v-if="player.isBot" :label="t('common.bot')" variant="default" />
      <Badge v-if="!player.isAlive" :label="t('common.eliminated')" variant="zombie" />
    </div>
  </button>
</template>
