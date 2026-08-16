<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RoomMood } from '@/types/enums'

const props = withDefaults(
  defineProps<{
    mood?: RoomMood | null
    compact?: boolean
  }>(),
  { mood: RoomMood.Safe, compact: false },
)

const { t } = useI18n()

const levels = [
  { id: RoomMood.Safe, key: 'safe', color: 'rgb(var(--color-room-mood-safe-rgb))' },
  { id: RoomMood.Suspicious, key: 'suspicious', color: 'rgb(var(--color-room-mood-suspicious-rgb))' },
  { id: RoomMood.Danger, key: 'danger', color: 'rgb(var(--color-room-mood-danger-rgb))' },
  { id: RoomMood.Critical, key: 'critical', color: 'rgb(var(--color-room-mood-critical-rgb))' },
] as const

const active = computed(() => props.mood ?? RoomMood.Safe)
const activeMeta = computed(() => levels.find((l) => l.id === active.value) ?? levels[0])
const markerPercent = computed(() => (active.value / (levels.length - 1)) * 100)
</script>

<template>
  <section class="mood" :class="{ 'mood--compact': compact }">
    <div class="mood-head">
      <span class="mood-label">{{ t('roomMood.title') }}</span>
      <span class="mood-value" :style="{ color: activeMeta.color }">
        {{ t(`roomMood.${activeMeta.key}`) }}
      </span>
    </div>
    <div class="mood-track">
      <div
        v-for="level in levels"
        :key="level.id"
        class="mood-seg"
        :style="{ background: level.color }"
        :class="{ 'mood-seg--active': level.id === active }"
      />
      <span class="mood-marker" :style="{ left: `${markerPercent}%` }" />
    </div>
    <div v-if="!compact" class="mood-legend">
      <span v-for="level in levels" :key="`leg-${level.id}`">{{ t(`roomMood.${level.key}`) }}</span>
    </div>
  </section>
</template>

<style scoped>
.mood {
  width: 100%;
}

.mood-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.5rem;
  margin-bottom: 0.4rem;
}

.mood-label {
  font-size: 0.7rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgb(255 226 170 / 0.75);
}

.mood-value {
  font-size: 0.85rem;
  font-weight: 900;
  text-transform: uppercase;
}

.mood-track {
  position: relative;
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 3px;
  height: 0.85rem;
  border-radius: 9999px;
  overflow: visible;
  background: rgb(15 8 4 / 0.55);
  border: 1px solid rgb(0 0 0 / 0.35);
}

.mood-seg {
  height: 100%;
  opacity: 0.45;
}

.mood-seg:first-child {
  border-radius: 9999px 0 0 9999px;
}

.mood-seg:last-child {
  border-radius: 0 9999px 9999px 0;
}

.mood-seg--active {
  opacity: 1;
}

.mood-marker {
  position: absolute;
  top: 50%;
  width: 1.1rem;
  height: 1.1rem;
  border-radius: 9999px;
  background: rgb(var(--color-game-title-rgb));
  border: 2px solid rgb(var(--color-on-game-rgb));
  box-shadow: 0 2px 0 rgb(0 0 0 / 0.45);
  transform: translate(-50%, -50%);
  pointer-events: none;
}

.mood-legend {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.2rem;
  margin-top: 0.35rem;
  font-size: 0.55rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.02em;
  color: rgb(255 226 170 / 0.65);
  text-align: center;
}

.mood--compact .mood-track {
  height: 0.65rem;
}
</style>
