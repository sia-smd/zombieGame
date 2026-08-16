<script setup lang="ts">
import Card from '@/components/common/Card/Card.vue'

defineProps<{
  filled?: boolean
  selected?: boolean
  disabled?: boolean
  label?: string
}>()
</script>

<template>
  <Card
    variant="game"
    padding="sm"
    :selected="selected"
    :hoverable="!disabled"
    class="flex aspect-square w-full items-center justify-center"
    :class="[
      filled ? 'border-secondary/30 bg-secondary/5' : 'border-dashed border-border/20',
      disabled ? 'opacity-disabled' : '',
    ]"
  >
    <slot>
      <span v-if="label" class="text-caption text-text-muted">{{ label }}</span>
    </slot>
  </Card>
</template>
