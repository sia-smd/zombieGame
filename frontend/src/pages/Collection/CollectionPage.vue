<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import GameLayout from '@/components/layout/GameLayout/GameLayout.vue'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import CardComponent from '@/components/battle/Card/Card.vue'

const { t } = useI18n()

const demoCards = computed(() => [
  { id: 'shield', name: t('cards.shield'), type: t('cards.defense') },
  { id: 'heal', name: t('cards.heal'), type: t('cards.support') },
  { id: 'shotgun', name: t('cards.shotgun'), type: t('cards.attack') },
  { id: 'human', name: t('cards.human'), type: t('cards.role') },
])
</script>

<template>
  <MobileFrame>
    <GameLayout :title="t('collection.title')" show-back>
      <div class="grid grid-cols-2 gap-3">
        <CardComponent
          v-for="card in demoCards"
          :key="card.id"
          :card-id="card.id"
          :name="card.name"
          :type="card.type"
        />
      </div>
    </GameLayout>
  </MobileFrame>
</template>
