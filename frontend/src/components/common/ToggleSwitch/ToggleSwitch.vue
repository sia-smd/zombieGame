<script setup lang="ts">
withDefaults(
  defineProps<{
    modelValue: boolean
    ariaLabel: string
    disabled?: boolean
    size?: 'sm' | 'md'
  }>(),
  {
    disabled: false,
    size: 'md',
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  change: [value: boolean]
}>()

function toggle(value: boolean) {
  const next = !value
  emit('update:modelValue', next)
  emit('change', next)
}
</script>

<template>
  <button
    type="button"
    role="switch"
    class="toggle-switch"
    :class="[`toggle-switch--${size}`, { 'toggle-switch--on': modelValue }]"
    :aria-label="ariaLabel"
    :aria-checked="modelValue"
    :disabled="disabled"
    @click="toggle(modelValue)"
  >
    <span class="toggle-switch__knob" aria-hidden="true" />
  </button>
</template>

<style scoped>
.toggle-switch {
  display: grid;
  flex-shrink: 0;
  align-items: center;
  padding: 0.125rem;
  border-radius: var(--radius-pill);
  background: rgb(var(--color-game-panel-rgb));
  border: 2px solid rgb(var(--color-game-border-rgb) / 0.7);
  transition: background 0.2s ease, border-color 0.2s ease;
}

.toggle-switch--sm {
  width: 3rem;
  height: 1.65rem;
}

.toggle-switch--md {
  width: 3.5rem;
  height: 1.9rem;
}

.toggle-switch--on {
  background: linear-gradient(
    180deg,
    rgb(var(--color-game-cta-start-rgb)) 0%,
    rgb(var(--color-game-cta-end-rgb)) 100%
  );
  border-color: rgb(var(--color-game-cta-border-rgb) / 0.8);
}

.toggle-switch__knob {
  grid-area: 1 / 1;
  justify-self: start;
  height: 1.15rem;
  width: 1.15rem;
  border-radius: var(--radius-pill);
  background: rgb(var(--color-text-primary-rgb));
  box-shadow: 0 2px 4px rgb(var(--color-bg-rgb) / 0.35);
  transition: transform 0.2s ease;
}

.toggle-switch--md .toggle-switch__knob {
  height: 1.35rem;
  width: 1.35rem;
}

.toggle-switch--on .toggle-switch__knob {
  transform: translateX(calc(100% + 0.35rem));
}

[dir='rtl'] .toggle-switch--on .toggle-switch__knob {
  transform: translateX(calc(-100% - 0.35rem));
}

.toggle-switch:disabled {
  cursor: not-allowed;
  opacity: var(--opacity-disabled);
}
</style>
