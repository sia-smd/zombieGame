<script setup lang="ts">
import { computed } from 'vue'
import { getCardImage } from '@/utils/imageAssets'

const props = defineProps<{
  cardId: string
  name: string
  type?: string
  disabled?: boolean
  selected?: boolean
}>()

defineEmits<{ play: [cardId: string] }>()

const cardImage = computed(() => getCardImage(props.cardId, props.name))
</script>

<template>
  <button
    type="button"
    class="w-28 shrink-0"
    :disabled="disabled"
    @click="$emit('play', cardId)"
  >
    <div
      class="relative overflow-hidden rounded-xl ring-1 transition-transform"
      :class="[
        selected ? 'ring-secondary shadow-glow-warning -translate-y-1' : 'ring-border/20',
        disabled ? 'opacity-40' : 'hover:-translate-y-1 active:scale-95',
      ]"
    >
      <img
        :src="cardImage"
        :alt="name"
        class="aspect-[5/7] w-full object-cover"
        loading="lazy"
      />
      <div class="absolute inset-x-0 bottom-0 bg-gradient-to-t from-bg/90 to-transparent px-1.5 pb-1.5 pt-6">
        <p v-if="type" class="truncate text-[9px] uppercase tracking-wider text-text-secondary">{{ type }}</p>
        <p class="truncate text-[11px] font-semibold text-text-primary">{{ name }}</p>
      </div>
    </div>
  </button>
</template>
