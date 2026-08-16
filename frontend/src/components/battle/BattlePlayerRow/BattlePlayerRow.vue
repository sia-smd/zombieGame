<script setup lang="ts">
const props = withDefaults(
  defineProps<{
    name: string
    avatar: string
    variant: 'me' | 'enemy'
    slotLabel: string
    droppable?: boolean
    roleImage?: string | null
    actionsLabel?: string | null
  }>(),
  { droppable: false, roleImage: null, actionsLabel: null },
)

const emit = defineEmits<{
  dragover: [event: DragEvent]
  drop: [event: DragEvent]
}>()

function onDragOver(event: DragEvent) {
  if (!props.droppable) return
  emit('dragover', event)
}

function onDrop(event: DragEvent) {
  if (!props.droppable) return
  emit('drop', event)
}
</script>

<template>
  <section class="battle-row px-3" :class="variant === 'enemy' ? 'pt-2' : ''">
    <div class="flex items-start gap-3">
      <div class="flex w-[4.5rem] flex-col items-center gap-1">
        <div class="avatar-frame" :class="variant === 'enemy' ? 'avatar-frame--enemy' : 'avatar-frame--me'">
          <img :src="avatar" :alt="name" class="h-full w-full object-cover" />
        </div>
        <span class="player-name" :class="variant === 'enemy' ? 'player-name--enemy' : 'player-name--me'">
          {{ name }}
        </span>
        <div v-if="roleImage" class="card-slot card-slot--role">
          <img :src="roleImage" alt="" class="card-img" />
        </div>
        <p v-if="actionsLabel" class="actions-left">{{ actionsLabel }}</p>
      </div>

      <div class="flex-1">
        <p class="slot-label">{{ slotLabel }}</p>
        <div
          class="play-table flex items-center justify-center gap-3"
          :class="{ 'play-table--drop': droppable }"
          @dragover="onDragOver"
          @drop="onDrop"
        >
          <slot />
        </div>
      </div>
    </div>
  </section>
</template>
