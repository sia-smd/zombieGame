<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import CardView from '@/components/battle/CardView/CardView.vue'
import type { PlayerCardState } from '@/types/api'

defineProps<{
  hand: PlayerCardState | null
  disabled?: boolean
  selectedCardId?: string | null
}>()

defineEmits<{ play: [cardId: string] }>()

const { t } = useI18n()

function slotCards(hand: PlayerCardState | null): Array<{ id: string; name: string }> {
  if (!hand) return []
  const cards: Array<{ id: string; name: string }> = []
  if (hand.roleCardId && hand.roleCardId !== '00000000-0000-0000-0000-000000000000') {
    cards.push({ id: hand.roleCardId, name: t('game.role') })
  }
  if (hand.inventorySlot1) cards.push({ id: hand.inventorySlot1, name: t('game.slot', { n: 1 }) })
  if (hand.inventorySlot2) cards.push({ id: hand.inventorySlot2, name: t('game.slot', { n: 2 }) })
  return cards.filter((c) => !hand.disabledCardIds.includes(c.id))
}
</script>

<template>
  <div class="flex gap-2 overflow-x-auto pb-2">
    <CardView
      v-for="card in slotCards(hand)"
      :key="card.id"
      :card-id="card.id"
      :name="card.name"
      :disabled="disabled"
      :selected="selectedCardId === card.id"
      @play="$emit('play', $event)"
    />
    <p v-if="!slotCards(hand).length" class="text-sm text-mist">{{ t('game.noCards') }}</p>
  </div>
</template>
