<script setup lang="ts">
import { computed } from 'vue'
import { images } from '@/assets/images'

defineOptions({ inheritAttrs: false })

const props = withDefaults(
  defineProps<{
    texture?: 'wood02' | 'wood03'
    as?: string
  }>(),
  {
    texture: 'wood03',
    as: 'div',
  },
)

const textureUrl = computed(() => images.ui[props.texture])
</script>

<template>
  <component
    :is="as"
    v-bind="$attrs"
    class="wood-panel"
    :class="`wood-panel--${texture}`"
    :style="{ backgroundImage: `url(${textureUrl})` }"
  >
    <slot />
  </component>
</template>

<style scoped>
.wood-panel {
  background-color: rgb(var(--color-game-panel-rgb));
}

.wood-panel--wood02 {
  background-repeat: repeat-x;
  background-position: center;
  background-size: 220px 100%;
}

.wood-panel--wood03 {
  background-repeat: repeat;
  background-size: 180px auto;
}
</style>
