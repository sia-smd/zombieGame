<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Button from '@/components/common/Button/Button.vue'

defineProps<{
  canPass?: boolean
  canPlay?: boolean
  loading?: boolean
}>()

defineEmits<{ pass: []; play: [] }>()

const { t } = useI18n()
</script>

<template>
  <div class="flex gap-2">
    <Button variant="ghost" class="flex-1" :disabled="!canPass || loading" @click="$emit('pass')">
      {{ t('battle.pass') }}
    </Button>
    <Button class="flex-1" :disabled="!canPlay || loading" :loading="loading" @click="$emit('play')">
      {{ t('common.play') }}
    </Button>
  </div>
</template>
