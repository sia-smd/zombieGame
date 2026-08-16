<script setup lang="ts">
import ChatWindow from '@/components/chat/ChatWindow/ChatWindow.vue'
import ChatInput from '@/components/chat/ChatInput/ChatInput.vue'
import type { ChatMessageDto } from '@/types/api'

defineProps<{
  messages: ChatMessageDto[]
  disabled?: boolean
}>()

defineEmits<{ send: [text: string] }>()
</script>

<template>
  <div class="space-y-2">
    <ChatWindow :messages="messages" />
    <ChatInput :disabled="disabled" @send="$emit('send', $event)" />
  </div>
</template>
