<script setup lang="ts">
import Badge from '@/components/common/Badge/Badge.vue'
import { useGameLabels } from '@/composables/useGameLabels'
import { PlayerRole } from '@/types/enums'

const props = defineProps<{ role: number }>()

const { roleLabel } = useGameLabels()

function roleBadgeVariant(role: number): 'human' | 'zombie' | 'default' {
  if (role === PlayerRole.Human) return 'human'
  if (role === PlayerRole.Zombie || role === PlayerRole.PowerZombie) return 'zombie'
  return 'default'
}
</script>

<template>
  <Badge :label="roleLabel(props.role)" :variant="roleBadgeVariant(props.role)" />
</template>
