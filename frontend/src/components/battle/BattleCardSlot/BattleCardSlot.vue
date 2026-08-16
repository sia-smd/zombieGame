<script setup lang="ts">
withDefaults(
  defineProps<{
    src?: string | null
    variant?: 'table' | 'hand' | 'role'
    selected?: boolean
    flip?: boolean
    disabled?: boolean
    interactive?: boolean
  }>(),
  { variant: 'table', src: null },
)
</script>

<template>
  <component
    :is="interactive ? 'button' : 'div'"
    :type="interactive ? 'button' : undefined"
    class="card-slot"
    :class="{
      'card-slot--table': variant === 'table',
      'card-slot--hand': variant === 'hand',
      'card-slot--role': variant === 'role',
      'card-slot--selected': selected,
      'card-slot--flip': flip,
    }"
    :disabled="interactive ? disabled : undefined"
  >
    <img v-if="src" :src="src" alt="" class="card-img" />
  </component>
</template>
