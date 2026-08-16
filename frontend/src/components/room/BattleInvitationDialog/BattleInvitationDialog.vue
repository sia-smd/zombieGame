<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { RoomInvitationDto } from '@/types/api'
import { images } from '@/assets/images'
import { playerAvatarUrl } from '@/utils/playerAvatar'
import Dialog from '@/components/common/Dialog/Dialog.vue'
import Button from '@/components/common/Button/Button.vue'
import { useCountdown } from '@/composables/useAnimation'

const props = defineProps<{
  invitation: RoomInvitationDto | null
  loading?: boolean
}>()

const emit = defineEmits<{ accept: [invitationId: string]; reject: [invitationId: string] }>()

const { t } = useI18n()
const invitationId = computed(() => props.invitation?.id ?? '')
const { secondsLeft } = useCountdown(
  () => props.invitation?.expiresAt,
  () => !!props.invitation,
)
</script>

<template>
  <Dialog
    :open="!!invitation"
    :title="t('invitation.title')"
    persistent
    surface="wood"
    align="center"
    max-width="sm"
  >
    <div v-if="invitation" class="invite-body">
      <div class="invite-art">
        <img :src="images.ui.matchmaking" alt="" class="invite-art__badge" />
        <img
          :src="playerAvatarUrl(invitation.fromImageId)"
          :alt="invitation.fromUsername"
          class="invite-art__avatar"
        />
      </div>

      <p class="invite-name">{{ invitation.fromUsername }}</p>
      <p class="invite-text">
        {{ t('invitation.message', { name: invitation.fromUsername }) }}
      </p>
      <p class="invite-text invite-text--question">{{ t('invitation.question') }}</p>

      <p v-if="secondsLeft !== null" class="invite-countdown">
        {{ t('invitation.expiresIn', { n: secondsLeft }) }}
      </p>

      <div class="invite-actions">
        <Button
          variant="success"
          class="invite-btn"
          :loading="loading"
          @click="emit('accept', invitationId)"
        >
          {{ t('invitation.accept') }}
        </Button>
        <Button
          variant="danger"
          class="invite-btn"
          :disabled="loading"
          @click="emit('reject', invitationId)"
        >
          {{ t('invitation.reject') }}
        </Button>
      </div>
    </div>
  </Dialog>
</template>

<style scoped>
:deep(.ds-title) {
  margin: 0;
  padding: 1rem 1.1rem 0;
  text-align: center;
  font-size: 1.2rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: rgb(var(--color-game-accent-rgb));
  text-shadow: 0 0 2px rgb(var(--color-on-game-rgb)), 0 2px 0 rgb(0 0 0 / 0.8);
}

.invite-body {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 0.85rem 1.15rem 1.15rem;
  text-align: center;
}

.invite-art {
  position: relative;
  display: flex;
  width: 100%;
  justify-content: center;
  margin: 0.35rem 0 0.75rem;
}

.invite-art__badge {
  position: absolute;
  top: -0.15rem;
  width: 2.1rem;
  height: 2.1rem;
  object-fit: contain;
  filter: drop-shadow(0 2px 6px rgb(0 0 0 / 0.55));
}

.invite-art__avatar {
  width: 6.25rem;
  height: 6.25rem;
  margin-top: 0.85rem;
  border-radius: 1.1rem;
  object-fit: cover;
  background: rgb(20 10 6 / 0.85);
  border: 3px solid rgb(255 207 107 / 0.75);
  box-shadow:
    0 0 16px rgb(255 207 107 / 0.25),
    0 8px 18px rgb(0 0 0 / 0.45);
}

.invite-name {
  margin: 0;
  font-size: 1.05rem;
  font-weight: 800;
  color: rgb(var(--color-on-game-rgb));
  text-shadow: 0 1px 0 rgb(0 0 0 / 0.7);
}

.invite-text {
  margin: 0.45rem 0 0;
  max-width: 16rem;
  font-size: 0.88rem;
  font-weight: 600;
  line-height: 1.55;
  color: rgb(255 255 255 / 0.92);
}

.invite-text--question {
  margin-top: 0.3rem;
  color: rgb(255 226 170 / 0.9);
}

.invite-countdown {
  margin-top: 0.75rem;
  min-width: 5.5rem;
  padding: 0.3rem 0.9rem;
  border-radius: 9999px;
  background: rgb(15 8 4 / 0.75);
  border: 2px solid rgb(180 130 70 / 0.7);
  font-size: 0.8rem;
  font-weight: 800;
  color: rgb(var(--color-game-title-rgb));
}

.invite-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.65rem;
  width: 100%;
  margin-top: 0.95rem;
}

.invite-btn {
  height: auto !important;
  min-height: 2.7rem;
  width: 100%;
  border-radius: 0.7rem;
  font-size: 0.9rem;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
</style>
