<script setup lang="ts">
defineProps<{
  modelValue?: string
  placeholder?: string
  disabled?: boolean
  rows?: number
  maxlength?: number
  error?: string
  ariaLabel?: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div class="w-full">
    <textarea
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :rows="rows ?? 3"
      :maxlength="maxlength"
      :aria-label="ariaLabel"
      :aria-invalid="!!error"
      class="ds-input min-h-[5.5rem] resize-none py-md"
      :class="error ? 'border-danger/50' : ''"
      @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
    />
    <p v-if="error" class="mt-xs text-caption text-danger" role="alert">{{ error }}</p>
  </div>
</template>
