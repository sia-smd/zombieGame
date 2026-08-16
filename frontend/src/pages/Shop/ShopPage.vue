<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import GameLayout from '@/components/layout/GameLayout/GameLayout.vue'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import Card from '@/components/common/Card/Card.vue'
import Button from '@/components/common/Button/Button.vue'
import { useSettingsStore } from '@/stores/settings.store'
import { useLocaleFormat } from '@/composables/useGameLabels'
import { images } from '@/assets/images'

const settings = useSettingsStore()
const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()

const packs = computed(() => [
  { id: '1', name: t('shop.survivorPack'), price: 500, icon: '🎁' },
  { id: '2', name: t('shop.bloodMoonBundle'), price: 1200, icon: '🌙' },
  { id: '3', name: t('shop.goldenRelics'), price: 2500, icon: '👑' },
])
</script>

<template>
  <MobileFrame>
    <GameLayout :title="t('shop.title')">
      <div class="space-y-3">
        <Card
          v-for="pack in packs"
          :key="pack.id"
          padding="md"
          glow="gold"
          class="flex items-center gap-3"
        >
          <span class="text-3xl">{{ pack.icon }}</span>
          <div class="flex-1">
            <p class="font-semibold text-fog">{{ pack.name }}</p>
            <p class="flex items-center gap-1 text-xs text-gold">
              <img :src="images.ui.coin" alt="" class="h-4 w-4" aria-hidden="true" />
              {{ t('common.coins', { n: formatLocaleNumber(pack.price) }) }}
            </p>
          </div>
          <Button
            size="sm"
            @click="settings.pushToast('info', t('shop.billingSoon'))"
          >
            {{ t('common.buy') }}
          </Button>
        </Card>
      </div>
    </GameLayout>
  </MobileFrame>
</template>
