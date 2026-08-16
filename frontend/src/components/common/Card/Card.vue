<script setup lang="ts">
import type { CardGlow, CardPadding, CardVariant } from '@/types/design-tokens'
import { computed } from 'vue'

type GlowProp = CardGlow | 'red' | 'green' | 'gold'

const props = withDefaults(
  defineProps<{
    variant?: CardVariant
    padding?: CardPadding
    glow?: GlowProp
    hoverable?: boolean
    selected?: boolean
    interactive?: boolean
  }>(),
  {
    variant: 'default',
    padding: 'md',
    glow: 'none',
  },
)

const paddingClass: Record<CardPadding, string> = {
  sm: 'p-md',
  md: 'p-lg',
  lg: 'p-xl',
}

const variantClass: Record<CardVariant, string> = {
  default: 'ds-panel',
  glass: 'rounded-3xl border border-border/10 bg-surface/60 shadow-md backdrop-blur-lg',
  outlined: 'rounded-3xl border-2 border-border/20 bg-transparent shadow-sm',
  popup: 'rounded-3xl border border-secondary/20 bg-surface shadow-lg',
  game: 'rounded-2xl border border-border/10 bg-card/80 shadow-md',
}

const glowClass: Record<CardGlow, string> = {
  none: '',
  primary: 'shadow-glow-primary',
  danger: 'shadow-glow-danger',
  success: 'shadow-glow-success',
  warning: 'shadow-glow-warning',
  secondary: 'shadow-glow-warning',
}

const legacyGlow: Record<string, CardGlow> = {
  red: 'danger',
  green: 'success',
  gold: 'secondary',
}

const resolvedGlow = computed<CardGlow>(() => {
  const g = props.glow
  if (g in legacyGlow) return legacyGlow[g]
  return g as CardGlow
})
</script>

<template>
  <component
    :is="interactive ? 'button' : 'div'"
    :type="interactive ? 'button' : undefined"
    class="text-start transition-all duration-fast"
    :class="[
      variantClass[variant],
      paddingClass[padding],
      glowClass[resolvedGlow],
      hoverable ? 'hover:-translate-y-0.5 hover:shadow-lg' : '',
      selected ? 'ring-2 ring-secondary/50' : '',
      interactive ? 'w-full active:scale-[0.98] focus-visible:ring-secondary' : '',
    ]"
  >
    <slot />
  </component>
</template>
