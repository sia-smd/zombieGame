<script setup lang="ts">
import Dialog from '@/components/common/Dialog/Dialog.vue'
import { useCountdown } from '@/composables/useAnimation'

const props = defineProps<{
  open: boolean
  title: string
  subtitle?: string
  imageSrc?: string
  imageFallbackSrc?: string
  endsAt?: string | null
}>()

const { secondsLeft } = useCountdown(() => props.endsAt, () => props.open)

function onImageError(event: Event) {
  const img = event.target as HTMLImageElement
  if (props.imageFallbackSrc && img.src !== props.imageFallbackSrc) {
    img.src = props.imageFallbackSrc
  }
}
</script>

<template>
  <Dialog
    :open="open"
    :title="title"
    persistent
    surface="wood"
    align="center"
    :max-width="imageSrc ? 'sm' : 'compact'"
    class="phase-dialog px-4 py-4 text-center"
  >
    <img
      v-if="imageSrc"
      :src="imageSrc"
      alt=""
      class="phase-dialog__art mx-auto mt-2"
      @error="onImageError"
    />
    <p v-if="subtitle" class="mt-2 text-sm text-amber-100/80">{{ subtitle }}</p>
    <div class="phase-dialog__clock mt-5">{{ secondsLeft === null ? '--' : secondsLeft }}</div>
  </Dialog>
</template>

<style scoped>
.phase-dialog {
  overflow: hidden;
}

:deep(.ds-title) {
  margin-bottom: 0;
  font-family: var(--font-display);
  font-size: 1.25rem;
  font-weight: 800;
  line-height: 1.75rem;
  letter-spacing: 0.1em;
  text-transform: uppercase;
  color: rgb(var(--color-game-title-rgb));
  text-shadow:
    0 0 2px rgb(var(--color-on-game-rgb)),
    0 2px 0 rgb(0 0 0 / 0.7);
}

.phase-dialog__art {
  display: block;
  width: 100%;
  max-height: 9.5rem;
  object-fit: contain;
  border-radius: 0.65rem;
  border: 2px solid rgb(180 130 70 / 0.55);
  background: rgb(15 8 4 / 0.55);
}

.phase-dialog__clock {
  display: inline-flex;
  min-width: 4.75rem;
  align-items: center;
  justify-content: center;
  padding: 0.5rem 1.1rem;
  border-radius: 0.75rem;
  background: rgb(15 8 4 / 0.9);
  border: 2px solid rgb(180 130 70 / 0.75);
  color: rgb(var(--color-on-game-rgb));
  font-size: 2rem;
  font-weight: 900;
  line-height: 1;
  box-shadow: 0 4px 0 rgb(0 0 0 / 0.4);
}
</style>
