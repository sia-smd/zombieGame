<script setup lang="ts">
defineProps<{ fullscreen?: boolean }>()
</script>

<template>
  <div class="relative min-h-screen w-full bg-bg">
    <div
      class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_top,_rgb(var(--color-primary-rgb)/0.12),_transparent_55%),radial-gradient(ellipse_at_bottom,_rgb(var(--color-success-rgb)/0.08),_transparent_50%)]"
    />
    <div
      class="relative mx-auto flex min-h-screen w-full flex-col"
      :class="fullscreen ? 'max-w-mobile' : 'max-w-mobile lg:my-2xl lg:min-h-mobile lg:rounded-3xl lg:border lg:border-border/10 lg:bg-surface lg:shadow-lg'"
    >
      <slot />
    </div>
  </div>
</template>
