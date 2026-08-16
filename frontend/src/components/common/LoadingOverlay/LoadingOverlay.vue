<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Spinner from '@/components/common/Spinner/Spinner.vue'

defineProps<{ message?: string; visible?: boolean }>()

const { t } = useI18n()
</script>

<template>
  <Teleport to="body">
    <Transition name="fade">
      <div
        v-if="visible"
        class="fixed inset-0 z-[60] flex items-center justify-center bg-overlay/overlay"
        aria-busy="true"
        aria-live="polite"
        @click.prevent
      >
        <div class="game-panel flex flex-col items-center gap-4 p-8">
          <Spinner size="lg" />
          <p class="text-sm text-mist">{{ message ?? t('common.loading') }}</p>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
