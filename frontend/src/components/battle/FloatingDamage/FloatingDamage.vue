<script setup lang="ts">
defineProps<{ amount: number; visible?: boolean }>()
</script>

<template>
  <Transition name="pop">
    <span
      v-if="visible"
      class="pointer-events-none absolute -top-2 left-1/2 -translate-x-1/2 rounded-full bg-blood px-2 py-0.5 text-xs font-bold text-white shadow-glow shadow-glow-red"
    >
      -{{ amount }}
    </span>
  </Transition>
</template>

<style scoped>
.pop-enter-active { animation: card-play 0.45s ease-out; }
</style>
