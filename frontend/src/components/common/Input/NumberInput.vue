<script setup lang="ts">
import Input from '@/components/common/Input/Input.vue'

defineProps<{
  modelValue?: number | string
  min?: number
  max?: number
  step?: number
  placeholder?: string
  disabled?: boolean
  error?: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <Input
    type="number"
    :model-value="modelValue"
    :min="min"
    :max="max"
    :step="step"
    :placeholder="placeholder"
    :disabled="disabled"
    :error="error"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
