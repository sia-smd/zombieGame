<script setup lang="ts">
defineProps<{
  label: string
  hint?: string
  emptyText: string
  statusText: string
  statusMuted?: boolean
  cards: Array<{ slotIndex: number; id: string; image: string; disabled: boolean }>
  emptySlots: number
  selectedSlot: number | null
  locked: boolean
}>()

defineEmits<{
  select: [slotIndex: number]
  dragstart: [event: DragEvent, slotIndex: number]
}>()
</script>

<template>
  <section class="px-3 pb-2">
    <p class="slot-label mb-1">{{ label }}</p>
    <p v-if="hint" class="mb-1 text-center text-[0.65rem] text-amber-200/80">{{ hint }}</p>
    <div class="flex items-center justify-center gap-2">
      <template v-if="cards.length">
        <button
          v-for="card in cards"
          :key="`${card.slotIndex}-${card.id}`"
          type="button"
          class="card-slot card-slot--hand"
          :class="{ 'card-slot--selected': selectedSlot === card.slotIndex }"
          :disabled="locked || card.disabled"
          draggable="true"
          @click="$emit('select', card.slotIndex)"
          @dragstart="$emit('dragstart', $event, card.slotIndex)"
        >
          <img :src="card.image" alt="" class="card-img" />
        </button>
        <div v-for="n in emptySlots" :key="`empty-${n}`" class="card-slot" />
      </template>
      <p v-else class="w-full py-3 text-center text-xs text-amber-100/70">{{ emptyText }}</p>
    </div>
    <p
      v-if="statusText"
      class="mt-1 text-center"
      :class="statusMuted ? 'text-[0.65rem] text-amber-100/60' : 'text-xs font-semibold text-amber-200'"
    >
      {{ statusText }}
    </p>
  </section>
</template>
