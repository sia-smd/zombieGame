<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Card from '@/components/common/Card/Card.vue'
import Button from '@/components/common/Button/Button.vue'
import { useLocaleFormat } from '@/composables/useGameLabels'

defineProps<{
  open: boolean
  title: string
  coins?: number
  xp?: number
}>()

defineEmits<{ close: [] }>()

const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()
</script>

<template>
  <Teleport to="body">
    <Transition name="fade">
      <div v-if="open" class="fixed inset-0 z-[80] flex items-center justify-center bg-black/80 p-4">
        <Card padding="lg" glow="gold" class="w-full max-w-sm space-y-4 text-center animate-fade-in-up">
          <h2 class="game-title text-2xl">{{ title }}</h2>
          <div class="flex justify-center gap-6 text-sm">
            <div v-if="coins !== undefined">
              <p class="text-mist">{{ t('common.coinsLabel') }}</p>
              <p class="text-xl font-bold text-gold">+{{ formatLocaleNumber(coins) }}</p>
            </div>
            <div v-if="xp !== undefined">
              <p class="text-mist">{{ t('common.xp') }}</p>
              <p class="text-xl font-bold text-plague">+{{ formatLocaleNumber(xp) }}</p>
            </div>
          </div>
          <Button block @click="$emit('close')">{{ t('common.collect') }}</Button>
        </Card>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
