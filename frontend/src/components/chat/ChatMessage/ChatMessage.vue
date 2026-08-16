<script setup lang="ts">
import type { ChatMessageDto } from '@/types/api'

defineProps<{ message: ChatMessageDto }>()
</script>

<template>
  <div class="text-sm">
    <span class="font-semibold text-gold">{{ message.username }}</span>
    <span class="text-mist">: {{ message.text }}</span>
  </div>
</template>
