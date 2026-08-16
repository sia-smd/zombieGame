<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from '@/components/common/Button/Button.vue'
import { PaperAirplaneIcon } from '@heroicons/vue/24/solid'

defineProps<{ disabled?: boolean }>()
const emit = defineEmits<{ send: [text: string] }>()

const { t } = useI18n()
const text = ref('')

function submit() {
  const value = text.value.trim()
  if (!value) return
  emit('send', value)
  text.value = ''
}
</script>

<template>
  <form class="flex gap-sm" @submit.prevent="submit">
    <input
      v-model="text"
      type="text"
      maxlength="200"
      :placeholder="t('chat.placeholder')"
      class="ds-input flex-1"
      :disabled="disabled"
      :aria-label="t('chat.placeholder')"
    />
    <Button type="submit" size="md" :disabled="disabled || !text.trim()" :icon="PaperAirplaneIcon" />
  </form>
</template>
