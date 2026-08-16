<script setup lang="ts">
import CardView from '@/components/battle/CardView/CardView.vue'

defineProps<{
  cardId: string
  name: string
  description?: string
  disabled?: boolean
}>()

defineEmits<{ activate: [cardId: string] }>()
</script>

<template>
  <div class="space-y-1">
    <CardView
      :card-id="cardId"
      :name="name"
      type="Skill"
      :disabled="disabled"
      @play="$emit('activate', $event)"
    />
    <p v-if="description" class="text-center text-[10px] text-mist">{{ description }}</p>
  </div>
</template>
