<script setup lang="ts">
defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    src: string
    overlayOpacity?: number
    imageClass?: string
  }>(),
  {
    overlayOpacity: 0.55,
    imageClass: '',
  },
)
</script>

<template>
  <div v-bind="$attrs" class="relative">
    <img
      :src="src"
      alt=""
      aria-hidden="true"
      class="absolute inset-0 h-full w-full object-cover"
      :class="imageClass"
    />
    <div
      class="absolute inset-0"
      :style="{ backgroundColor: `rgb(var(--color-bg-rgb) / ${overlayOpacity})` }"
      aria-hidden="true"
    />
    <div class="relative z-10 flex min-h-0 min-w-0 flex-1 flex-col">
      <slot />
    </div>
  </div>
</template>
