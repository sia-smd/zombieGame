<script setup lang="ts">
import { playerInitials } from '@/composables/useGameLabels'

withDefaults(
  defineProps<{
    name: string
    imageUrl?: string | null
    size?: 'sm' | 'md' | 'lg' | 'xl'
    alive?: boolean
    ring?: 'human' | 'zombie' | 'gold' | 'none' | 'success' | 'danger' | 'secondary'
  }>(),
  { size: 'md' },
)

const sizeClass = { sm: 'h-10 w-10 text-caption', md: 'h-14 w-14 text-body', lg: 'h-20 w-20 text-subtitle', xl: 'h-28 w-28 text-subtitle' }
const ringClass = {
  human: 'ring-2 ring-success shadow-glow-success',
  zombie: 'ring-2 ring-danger shadow-glow-danger',
  gold: 'ring-2 ring-secondary shadow-glow-warning',
  secondary: 'ring-2 ring-secondary shadow-glow-warning',
  success: 'ring-2 ring-success shadow-glow-success',
  danger: 'ring-2 ring-danger shadow-glow-danger',
  none: 'ring-1 ring-border/10',
}
</script>

<template>
  <div
    class="relative shrink-0 overflow-hidden rounded-circle bg-card"
    :class="[sizeClass[size], ringClass[ring ?? 'none'], alive === false ? 'opacity-disabled grayscale' : '']"
  >
    <img v-if="imageUrl" :src="imageUrl" :alt="name" class="h-full w-full object-cover" />
    <span v-else class="flex h-full w-full items-center justify-center font-semibold text-text-primary">
      {{ playerInitials(name) }}
    </span>
  </div>
</template>
