<script setup lang="ts">
import { computed, useAttrs } from 'vue'

defineOptions({ inheritAttrs: false })

const props = withDefaults(
  defineProps<{
    modelValue?: string | number
    type?: string
    placeholder?: string
    disabled?: boolean
    readonly?: boolean
    id?: string
    name?: string
    autocomplete?: string
    maxlength?: number
    error?: string
    ariaLabel?: string
  }>(),
  {
    type: 'text',
    autocomplete: 'off',
  },
)

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const attrs = useAttrs()

const inputId = computed(() => props.id ?? `input-${Math.random().toString(36).slice(2, 9)}`)
</script>

<template>
  <div class="w-full">
    <input
      :id="inputId"
      :value="modelValue"
      :type="type"
      :name="name"
      :placeholder="placeholder"
      :disabled="disabled"
      :readonly="readonly"
      :autocomplete="autocomplete"
      :maxlength="maxlength"
      :aria-label="ariaLabel"
      :aria-invalid="!!error"
      v-bind="attrs"
      class="ds-input"
      :class="error ? 'border-danger/50 focus:border-danger/50 focus:ring-danger/30' : ''"
      @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    />
    <p v-if="error" class="mt-xs text-caption text-danger" role="alert">{{ error }}</p>
  </div>
</template>
