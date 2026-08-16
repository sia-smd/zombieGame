<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { PlusIcon } from '@heroicons/vue/24/solid'
import type { RoomPlayerDto } from '@/types/api'
import type { RoomPlayerStatus } from '@/stores/room.store'
import { playerAvatarUrl } from '@/utils/playerAvatar'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'

const props = defineProps<{
  player: RoomPlayerDto
  status: RoomPlayerStatus
  canInvite?: boolean
  isSelf?: boolean
}>()

const emit = defineEmits<{ invite: [userId: string] }>()

const { t } = useI18n()

const statusLabel = computed(() => t(`room.status.${props.status}`))

function onClick() {
  if (!props.canInvite) return
  emit('invite', props.player.userId)
}
</script>

<template>
  <WoodPanel
    as="button"
    type="button"
    class="room-tile"
    :class="[
      `room-tile--${status}`,
      { 'room-tile--invitable': canInvite, 'room-tile--self': isSelf },
    ]"
    :disabled="!canInvite"
    @click="onClick"
  >
    <span class="room-tile__top">
      <img :src="playerAvatarUrl(player.imageId)" alt="" class="room-tile__avatar" aria-hidden="true" />
      <span class="room-tile__info">
        <span class="room-tile__name">{{ player.username }}</span>
        <span class="room-tile__life">
          <span class="room-tile__life-text">
            {{ player.isAlive ? t('room.status.alive') : t('room.status.dead') }}
          </span>
          <span class="room-tile__dot" />
        </span>
      </span>
    </span>

    <span class="room-tile__status">{{ statusLabel }}</span>

    <span v-if="canInvite" class="room-tile__invite" :aria-label="t('room.invite')">
      <PlusIcon class="h-3 w-3" />
    </span>
  </WoodPanel>
</template>

<style scoped>
.room-tile {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  width: 100%;
  padding: 0.35rem 0.35rem 0.3rem;
  border-radius: 0.7rem;
  background-repeat: repeat;
  background-size: 140px auto;
  background-color: rgb(var(--color-game-panel-rgb));
  border: 2px solid rgb(150 100 55 / 0.85);
  box-shadow: 0 3px 0 rgb(0 0 0 / 0.35);
  text-align: start;
}

.room-tile:disabled {
  cursor: default;
}

.room-tile__top {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.room-tile__avatar {
  height: 1.9rem;
  width: 1.9rem;
  flex-shrink: 0;
  border-radius: 9999px;
  object-fit: cover;
  background: rgb(var(--color-game-input-rgb));
  border: 2px solid rgb(var(--color-on-game-rgb) / 0.35);
}

.room-tile__info {
  display: flex;
  min-width: 0;
  flex-direction: column;
  line-height: 1.1;
}

.room-tile__name {
  overflow: hidden;
  font-size: 0.62rem;
  font-weight: 800;
  color: rgb(var(--color-on-game-rgb));
  text-overflow: ellipsis;
  white-space: nowrap;
}

.room-tile__life {
  display: inline-flex;
  align-items: center;
  gap: 0.2rem;
}

.room-tile__life-text {
  font-size: 0.5rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  color: rgb(255 255 255 / 0.7);
}

.room-tile__dot {
  height: 0.3rem;
  width: 0.3rem;
  border-radius: 9999px;
  background: rgb(var(--color-room-mood-safe-rgb));
  box-shadow: 0 0 4px rgb(74 222 128 / 0.9);
}

.room-tile__status {
  display: block;
  border-radius: 0.4rem;
  padding: 0.1rem 0.2rem;
  text-align: center;
  font-size: 0.48rem;
  font-weight: 800;
  letter-spacing: 0.04em;
  border: 1px solid rgb(255 255 255 / 0.2);
}

.room-tile__invite {
  position: absolute;
  top: -0.3rem;
  inset-inline-end: -0.3rem;
  display: inline-flex;
  height: 1.1rem;
  width: 1.1rem;
  align-items: center;
  justify-content: center;
  border-radius: 9999px;
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border: 2px solid rgb(255 255 255 / 0.8);
  color: rgb(var(--color-on-game-rgb));
}

/* Status skins mirror the room mockup: brown = free, blue = negotiating, red = out. */
.room-tile--available {
  border-color: rgb(226 140 60 / 0.9);
}

.room-tile--available .room-tile__status {
  background: rgb(120 70 30 / 0.85);
  color: rgb(var(--color-game-title-rgb));
}

.room-tile--inviting {
  border-color: rgb(96 190 255 / 0.95);
  box-shadow:
    0 3px 0 rgb(0 0 0 / 0.35),
    0 0 12px rgb(96 190 255 / 0.4);
}

.room-tile--inviting .room-tile__status {
  background: rgb(20 80 130 / 0.9);
  color: rgb(var(--color-info-rgb));
}

.room-tile--waiting {
  border-color: rgb(96 190 255 / 0.95);
}

.room-tile--waiting .room-tile__status {
  background: rgb(20 80 130 / 0.9);
  color: rgb(var(--color-info-rgb));
}

.room-tile--disconnected {
  border-color: rgb(150 150 150 / 0.7);
  filter: grayscale(0.6);
}

.room-tile--disconnected .room-tile__status {
  background: rgb(60 60 65 / 0.9);
  color: rgb(var(--color-text-secondary-rgb));
}

.room-tile--disconnected .room-tile__dot {
  background: rgb(var(--color-text-muted-rgb));
  box-shadow: none;
}

.room-tile--inBattle {
  border-color: rgb(226 140 60 / 0.9);
}

.room-tile--inBattle .room-tile__status {
  background: rgb(150 80 20 / 0.9);
  color: rgb(var(--color-game-title-rgb));
}

.room-tile--resting {
  border-color: rgb(160 140 90 / 0.85);
}

.room-tile--resting .room-tile__status {
  background: rgb(70 55 30 / 0.9);
  color: rgb(var(--color-game-accent-rgb));
}

.room-tile--eliminated {
  border-color: rgb(120 120 120 / 0.6);
  filter: grayscale(0.85);
  opacity: 0.75;
}

.room-tile--eliminated .room-tile__status {
  background: rgb(90 25 25 / 0.9);
  color: rgb(var(--color-room-mood-critical-rgb));
  text-decoration: line-through;
}

.room-tile--eliminated .room-tile__dot {
  background: rgb(var(--color-text-disabled-rgb));
  box-shadow: none;
}

.room-tile--invitable {
  border-color: rgb(155 232 74 / 0.95);
  box-shadow:
    0 3px 0 rgb(0 0 0 / 0.35),
    0 0 12px rgb(126 214 58 / 0.4);
}

.room-tile--self {
  outline: 2px solid rgb(255 179 71 / 0.8);
  outline-offset: 1px;
}
</style>
