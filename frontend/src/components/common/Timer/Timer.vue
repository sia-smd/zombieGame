<script setup lang="ts">
import { computed } from 'vue'
import { useCountdown, formatTimer } from '@/composables/useAnimation'

const props = defineProps<{
  endsAt?: string | null
  seconds?: number
  urgentBelow?: number
}>()

const { remaining } = useCountdown(() => props.endsAt ?? null)
const display = computed(() => (props.seconds !== undefined ? props.seconds : remaining.value))
const urgent = computed(
  () => display.value !== null && display.value <= (props.urgentBelow ?? 10),
)
</script>

<template>
  <div
    class="inline-flex min-w-[4.5rem] items-center justify-center rounded-2xl border px-3 py-1.5 font-mono text-sm font-bold tabular-nums"
    :class="urgent
      ? 'border-danger/50 bg-danger/20 text-danger animate-pulse-glow'
      : 'border-game-cta-border/30 bg-game-cta-start/10 text-game-accent'"
  >
    {{ display === null ? '--:--' : formatTimer(display) }}
  </div>
</template>
