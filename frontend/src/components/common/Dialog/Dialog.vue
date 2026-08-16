<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch, type ComponentPublicInstance } from 'vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'

defineOptions({ inheritAttrs: false })

let dialogId = 0

const props = withDefaults(
  defineProps<{
    open: boolean
    title?: string
    /** When true, Escape and backdrop click do not close the dialog. */
    persistent?: boolean
    surface?: 'default' | 'wood'
    align?: 'center' | 'bottom'
    maxWidth?: 'compact' | 'xs' | 'sm' | 'mobile' | 'md'
  }>(),
  {
    persistent: false,
    surface: 'default',
    align: 'bottom',
    maxWidth: 'mobile',
  },
)

const emit = defineEmits<{ close: [] }>()
const panel = ref<HTMLElement | ComponentPublicInstance | null>(null)
const titleId = `dialog-title-${++dialogId}`
let previousFocus: HTMLElement | null = null

const surfaceComponent = computed(() => (props.surface === 'wood' ? WoodPanel : 'div'))
const maxWidthClass = computed(() => ({
  compact: 'max-w-[18rem]',
  xs: 'max-w-xs',
  sm: 'max-w-sm',
  mobile: 'max-w-mobile',
  md: 'max-w-md',
})[props.maxWidth])

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape' && !props.persistent) emit('close')
}

function restoreFocus() {
  previousFocus?.focus()
  previousFocus = null
}

function panelElement() {
  const value = panel.value
  if (!value) return null
  return value instanceof HTMLElement ? value : value.$el as HTMLElement
}

watch(
  () => props.open,
  async (open) => {
    if (!open) {
      restoreFocus()
      return
    }
    previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null
    await nextTick()
    const element = panelElement()
    const firstFocusable = element?.querySelector<HTMLElement>(
      '[autofocus], button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
    )
    ;(firstFocusable ?? element)?.focus()
  },
  { flush: 'post' },
)

onMounted(() => document.addEventListener('keydown', onKey))
onUnmounted(() => {
  document.removeEventListener('keydown', onKey)
  restoreFocus()
})
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-fast ease-out"
      leave-active-class="transition-opacity duration-fast ease-in"
      enter-from-class="opacity-0"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-modal-backdrop flex justify-center bg-overlay/overlay p-lg"
        :class="align === 'center' ? 'items-center' : 'items-end sm:items-center'"
        role="presentation"
        @click.self="!persistent && emit('close')"
      >
        <component
          :is="surfaceComponent"
          ref="panel"
          v-bind="$attrs"
          class="animate-ds-fade-up z-modal w-full"
          :class="[
            maxWidthClass,
            {
              'ds-panel p-xl': surface === 'default',
              'game-dialog-surface': surface === 'wood',
            },
          ]"
          role="dialog"
          aria-modal="true"
          tabindex="-1"
          :aria-labelledby="title ? titleId : undefined"
        >
          <h2 v-if="title" :id="titleId" class="ds-title mb-md text-title">{{ title }}</h2>
          <slot />
          <div v-if="$slots.actions" class="mt-lg flex justify-end gap-sm">
            <slot name="actions" />
          </div>
        </component>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.game-dialog-surface {
  border-radius: 0.9rem;
  border: 3px solid rgb(var(--color-game-cta-border-rgb));
  box-shadow:
    0 0 0 2px rgb(var(--color-on-game-rgb) / 0.32),
    0 0 14px rgb(var(--color-game-cta-start-rgb) / 0.38),
    0 8px 20px rgb(var(--color-bg-rgb) / 0.5);
}
</style>
