<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import GameLayout from '@/components/layout/GameLayout/GameLayout.vue'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import Card from '@/components/common/Card/Card.vue'

const { t } = useI18n()
</script>

<template>
  <MobileFrame>
    <GameLayout :title="t('friends.title')" show-back>
      <Card padding="lg" class="text-center text-sm text-mist">
        {{ t('friends.comingSoon') }}
      </Card>
    </GameLayout>
  </MobileFrame>
</template>
