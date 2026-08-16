<script setup lang="ts">
defineProps<{ text: string }>()
</script>

<template>
  <div class="group relative inline-block">
    <slot />
    <div
      class="pointer-events-none absolute bottom-full start-1/2 z-20 mb-2 -translate-x-1/2 whitespace-nowrap rounded-xl bg-abyss px-3 py-1.5 text-xs text-fog opacity-0 shadow-card transition-opacity group-hover:opacity-100"
    >
      {{ text }}
    </div>
  </div>
</template>
