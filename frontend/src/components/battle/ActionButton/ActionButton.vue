<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Card from '@/components/common/Card/Card.vue'
import Button from '@/components/common/Button/Button.vue'

defineProps<{
  title: string
  subtitle?: string
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  icon?: string
  imageSrc?: string
  disabled?: boolean
}>()

defineEmits<{ action: [] }>()

const { t } = useI18n()
</script>

<template>
  <Card padding="md" class="flex items-center gap-3">
    <div class="flex h-12 w-12 items-center justify-center overflow-hidden rounded-2xl bg-black/40">
      <img v-if="imageSrc" :src="imageSrc" :alt="title" class="h-full w-full object-cover" />
      <span v-else class="text-xl">{{ icon ?? '⚔️' }}</span>
    </div>
    <div class="min-w-0 flex-1">
      <p class="font-semibold text-fog">{{ title }}</p>
      <p v-if="subtitle" class="text-xs text-mist">{{ subtitle }}</p>
    </div>
    <Button :variant="variant ?? 'primary'" size="sm" :disabled="disabled" @click="$emit('action')">
      {{ t('common.go') }}
    </Button>
  </Card>
</template>
