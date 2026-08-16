<script setup lang="ts">
import { MagnifyingGlassIcon } from '@heroicons/vue/24/outline'

defineProps<{
  modelValue?: string
  placeholder?: string
  disabled?: boolean
  ariaLabel?: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<template>
  <div class="relative">
    <MagnifyingGlassIcon
      class="pointer-events-none absolute start-lg top-1/2 h-5 w-5 -translate-y-1/2 text-text-muted"
      aria-hidden="true"
    />
    <input
      type="search"
      :value="modelValue"
      :placeholder="placeholder"
      :disabled="disabled"
      :aria-label="ariaLabel"
      class="ds-input ps-10"
      @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    />
  </div>
</template>
