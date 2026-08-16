<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'

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
      enter-active-class="transition-transform duration-slow ease-out"
      leave-active-class="transition-transform duration-fast ease-in"
      enter-from-class="translate-y-full"
      leave-to-class="translate-y-full"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-modal-backdrop flex items-end bg-overlay/overlay"
        role="presentation"
        @click.self="emit('close')"
      >
        <div
          class="ds-panel z-modal max-h-[85vh] w-full overflow-y-auto rounded-b-none p-xl animate-ds-slide-up"
          role="dialog"
          aria-modal="true"
          :aria-label="title"
        >
          <div class="mx-auto mb-lg h-1 w-12 rounded-pill bg-border/20" aria-hidden="true" />
          <h2 v-if="title" class="ds-title mb-md text-title">{{ title }}</h2>
          <slot />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
