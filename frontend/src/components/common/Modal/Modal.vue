<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import Card from '@/components/common/Card/Card.vue'

defineProps<{
  open: boolean
  title?: string
}>()

const emit = defineEmits<{ close: [] }>()

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') emit('close')
}

onMounted(() => document.addEventListener('keydown', onKey))
onUnmounted(() => document.removeEventListener('keydown', onKey))
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-fast ease-out"
      leave-active-class="transition-opacity duration-fast ease-in"
      enter-from-class="opacity-0"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-modal-backdrop flex items-center justify-center bg-overlay/overlay p-lg"
        role="presentation"
        @click.self="emit('close')"
      >
        <Card variant="popup" padding="lg" glow="secondary" class="animate-ds-zoom z-modal w-full max-w-sm">
          <h2 v-if="title" class="ds-title mb-md text-heading text-center">{{ title }}</h2>
          <slot />
          <div class="mt-lg flex justify-end gap-sm">
            <slot name="actions" />
          </div>
        </Card>
      </div>
    </Transition>
  </Teleport>
</template>
