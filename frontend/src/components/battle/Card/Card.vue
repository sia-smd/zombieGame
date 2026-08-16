<script setup lang="ts">
import CardView from '@/components/battle/CardView/CardView.vue'

defineProps<{
  cardId: string
  name: string
  type?: string
}>()
</script>

<template>
  <CardView :card-id="cardId" :name="name" :type="type" disabled />
</template>
