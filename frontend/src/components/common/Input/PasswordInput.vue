<script setup lang="ts">
import { ref } from 'vue'
import Input from '@/components/common/Input/Input.vue'
import IconButton from '@/components/common/IconButton/IconButton.vue'
import { EyeIcon, EyeSlashIcon } from '@heroicons/vue/24/outline'

defineProps<{
  modelValue?: string
  placeholder?: string
  disabled?: boolean
  error?: string
  ariaLabel?: string
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const visible = ref(false)
</script>

<template>
  <div class="relative w-full">
    <Input
      :model-value="modelValue"
      :type="visible ? 'text' : 'password'"
      :placeholder="placeholder"
      :disabled="disabled"
      :error="error"
      :aria-label="ariaLabel"
      @update:model-value="emit('update:modelValue', $event)"
    />
    <div class="absolute end-sm top-1/2 -translate-y-1/2">
      <IconButton
        :icon="visible ? EyeSlashIcon : EyeIcon"
        :label="visible ? 'Hide password' : 'Show password'"
        variant="ghost"
        size="sm"
        @click="visible = !visible"
      />
    </div>
  </div>
</template>
