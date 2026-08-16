<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { PaperAirplaneIcon } from '@heroicons/vue/24/solid'
import type { ChatMessageDto } from '@/types/api'
import { sameUserId } from '@/utils/ids'

const props = defineProps<{
  messages: ChatMessageDto[]
  myUserId?: string | null
  disabled?: boolean
}>()

const emit = defineEmits<{ send: [text: string] }>()

const { t } = useI18n()
const text = ref('')
const listRef = ref<HTMLElement | null>(null)

watch(
  () => props.messages.length,
  async () => {
    await nextTick()
    if (listRef.value) listRef.value.scrollTop = listRef.value.scrollHeight
  },
)

function submit() {
  const value = text.value.trim()
  if (!value) return
  emit('send', value)
  text.value = ''
}
</script>

<template>
  <div class="room-chat">
    <div ref="listRef" class="room-chat__list">
      <p v-if="!messages.length" class="room-chat__empty">{{ t('room.chatEmpty') }}</p>
      <p
        v-for="msg in messages"
        :key="msg.id"
        class="room-chat__msg"
        :class="{ 'room-chat__msg--mine': sameUserId(msg.userId, myUserId) }"
      >
        <span class="room-chat__author">{{ msg.username }}:</span>
        <span class="room-chat__text">{{ msg.text }}</span>
      </p>
    </div>

    <form class="room-chat__form" @submit.prevent="submit">
      <input
        v-model="text"
        type="text"
        maxlength="200"
        :placeholder="t('chat.placeholder')"
        :aria-label="t('chat.placeholder')"
        :disabled="disabled"
        class="room-chat__input"
      />
      <button
        type="submit"
        class="room-chat__send"
        :disabled="disabled || !text.trim()"
        :aria-label="t('common.send')"
      >
        <PaperAirplaneIcon class="h-4 w-4" />
      </button>
    </form>
  </div>
</template>

<style scoped>
.room-chat {
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
  gap: 0.4rem;
}

.room-chat__list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding-inline-end: 0.15rem;
}

.room-chat__empty {
  padding-block: 0.5rem;
  text-align: center;
  font-size: 0.7rem;
  color: rgb(255 255 255 / 0.5);
}

.room-chat__msg {
  font-size: 0.72rem;
  line-height: 1.45;
  color: rgb(255 255 255 / 0.9);
}

.room-chat__msg--mine .room-chat__author {
  color: rgb(var(--color-game-cta-start-rgb));
}

.room-chat__author {
  margin-inline-end: 0.25rem;
  font-weight: 800;
  color: rgb(var(--color-game-title-rgb));
}

.room-chat__form {
  display: flex;
  flex-shrink: 0;
  gap: 0.35rem;
}

.room-chat__input {
  min-width: 0;
  flex: 1;
  border: 2px solid rgb(150 100 55 / 0.85);
  border-radius: 0.6rem;
  background: rgb(20 10 6 / 0.6);
  padding: 0.4rem 0.6rem;
  font-size: 0.75rem;
  font-weight: 600;
  color: rgb(var(--color-on-game-rgb));
  outline: none;
}

.room-chat__input::placeholder {
  color: rgb(255 255 255 / 0.4);
}

.room-chat__send {
  display: inline-flex;
  height: 2rem;
  width: 2.25rem;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 0.6rem;
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border: 2px solid rgb(255 255 255 / 0.6);
  color: rgb(var(--color-on-game-rgb));
}

.room-chat__send:disabled {
  opacity: 0.5;
}
</style>
