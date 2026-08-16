<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Card from '@/components/common/Card/Card.vue'
import Avatar from '@/components/common/Avatar/Avatar.vue'

defineProps<{
  playerName: string
  selected?: boolean
  disabled?: boolean
}>()

defineEmits<{ vote: [] }>()

const { t } = useI18n()
</script>

<template>
  <button
    type="button"
    class="w-full"
    :disabled="disabled"
    @click="$emit('vote')"
  >
    <Card
      padding="md"
      :glow="selected ? 'red' : 'none'"
      class="flex flex-col items-center gap-2 transition active:scale-[0.98]"
    >
      <Avatar :name="playerName" :ring="selected ? 'zombie' : 'none'" />
      <span class="text-sm font-semibold text-fog">{{ playerName }}</span>
      <span class="text-xs text-blood">{{ t('common.vote') }}</span>
    </Card>
  </button>
</template>
