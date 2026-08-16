<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import GameLayout from '@/components/layout/GameLayout/GameLayout.vue'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import Card from '@/components/common/Card/Card.vue'
import Avatar from '@/components/common/Avatar/Avatar.vue'
import { useLocaleFormat } from '@/composables/useGameLabels'

const { t } = useI18n()
const { formatLocaleNumber } = useLocaleFormat()

const leaders = computed(() => [
  { name: 'ShadowHunter', score: 2840 },
  { name: 'PlagueLord', score: 2710 },
  { name: 'LastHope', score: 2655 },
  { name: 'NightWalker', score: 2500 },
  { name: t('leaderboard.you'), score: 1200 },
])
</script>

<template>
  <MobileFrame>
    <GameLayout :title="t('leaderboard.title')" show-back>
      <div class="space-y-2">
        <Card
          v-for="(entry, index) in leaders"
          :key="entry.name"
          padding="sm"
          class="flex items-center gap-3"
          :glow="index < 3 ? 'gold' : 'none'"
        >
          <span class="w-6 text-center font-bold text-gold">#{{ formatLocaleNumber(index + 1) }}</span>
          <Avatar :name="entry.name" size="sm" />
          <span class="flex-1 text-sm text-fog">{{ entry.name }}</span>
          <span class="text-sm font-semibold text-plague">{{ formatLocaleNumber(entry.score) }}</span>
        </Card>
      </div>
    </GameLayout>
  </MobileFrame>
</template>
