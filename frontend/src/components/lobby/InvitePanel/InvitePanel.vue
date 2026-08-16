<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Card from '@/components/common/Card/Card.vue'
import Avatar from '@/components/common/Avatar/Avatar.vue'
import Button from '@/components/common/Button/Button.vue'
import type { RoomPlayerDto } from '@/types/api'

defineProps<{
  opponents: RoomPlayerDto[]
  disabled?: boolean
}>()

defineEmits<{ invite: [userId: string] }>()

const { t } = useI18n()
</script>

<template>
  <Card padding="md" class="space-y-3">
    <h3 class="text-sm font-semibold text-gold">{{ t('lobby.inviteOpponent') }}</h3>
    <ul class="space-y-2">
      <li
        v-for="opponent in opponents"
        :key="opponent.userId"
        class="flex items-center justify-between gap-2 rounded-2xl bg-black/30 p-2"
      >
        <div class="flex items-center gap-2">
          <Avatar :name="opponent.username" size="sm" />
          <span class="text-sm text-fog">{{ opponent.username }}</span>
        </div>
        <Button size="sm" variant="secondary" :disabled="disabled" @click="$emit('invite', opponent.userId)">
          {{ t('common.invite') }}
        </Button>
      </li>
    </ul>
    <p v-if="!opponents.length" class="text-center text-xs text-mist">{{ t('lobby.noOpponents') }}</p>
  </Card>
</template>
