<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import ChatMessage from '@/components/chat/ChatMessage/ChatMessage.vue'
import type { ChatMessageDto } from '@/types/api'

defineProps<{ messages: ChatMessageDto[] }>()

const { t } = useI18n()
</script>

<template>
  <div class="game-panel flex max-h-56 flex-col gap-2 overflow-hidden p-3">
    <h3 class="text-xs font-semibold uppercase tracking-wider text-gold">{{ t('common.chat') }}</h3>
    <div class="flex-1 space-y-2 overflow-y-auto">
      <ChatMessage v-for="msg in messages" :key="msg.id" :message="msg" />
      <p v-if="!messages.length" class="text-center text-xs text-mist">{{ t('chat.empty') }}</p>
    </div>
  </div>
</template>
