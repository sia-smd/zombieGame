<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  modelValue?: string
  length?: number
  disabled?: boolean
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const digits = computed(() => {
  const len = props.length ?? 6
  const chars = (props.modelValue ?? '').split('')
  return Array.from({ length: len }, (_, i) => chars[i] ?? '')
})

function onInput(e: Event, index: number) {
  const input = e.target as HTMLInputElement
  const val = input.value.replace(/\D/g, '').slice(-1)
  const chars = digits.value.slice()
  chars[index] = val
  const next = chars.join('').slice(0, props.length ?? 6)
  emit('update:modelValue', next)

  if (val && input.nextElementSibling instanceof HTMLInputElement) {
    input.nextElementSibling.focus()
  }
}

function onKeydown(e: KeyboardEvent, index: number) {
  const input = e.target as HTMLInputElement
  if (e.key === 'Backspace' && !digits.value[index] && input.previousElementSibling instanceof HTMLInputElement) {
    input.previousElementSibling.focus()
  }
}
</script>

<template>
  <div class="flex justify-center gap-sm" role="group" aria-label="One-time password">
    <input
      v-for="(digit, index) in digits"
      :key="index"
      type="text"
      inputmode="numeric"
      maxlength="1"
      :value="digit"
      :disabled="disabled"
      class="ds-input h-12 w-10 text-center text-title"
      @input="onInput($event, index)"
      @keydown="onKeydown($event, index)"
    />
  </div>
</template>
