<script setup lang="ts">
import { computed, reactive } from 'vue'
import MobileFrame from '@/components/layout/MobileFrame/MobileFrame.vue'
import PageBackdrop from '@/components/layout/PageBackdrop/PageBackdrop.vue'
import WoodPanel from '@/components/layout/WoodPanel/WoodPanel.vue'
import LoadingOverlay from '@/components/common/LoadingOverlay/LoadingOverlay.vue'
import BattleCardSlot from '@/components/battle/BattleCardSlot/BattleCardSlot.vue'
import BattlePlayerRow from '@/components/battle/BattlePlayerRow/BattlePlayerRow.vue'
import BattleInventory from '@/components/battle/BattleInventory/BattleInventory.vue'
import { QuestionMarkCircleIcon } from '@heroicons/vue/24/outline'
import { useBattlePlay } from '@/composables/useBattlePlay'
import { getCardImage } from '@/utils/imageAssets'
import { images } from '@/assets/images'
import type { OpponentSlot } from '@/composables/useBattlePlay'

const props = defineProps<{ id: string }>()
const battle = reactive(useBattlePlay(() => props.id))

const inventoryLocked = computed(
  () => battle.isTurnOver || battle.actionLoading || battle.holdingForReveal,
)

const inventoryStatus = computed(() => {
  if (battle.isTurnOver && !battle.showResultBanner) {
    return { text: battle.t('battle.turnOver'), muted: false }
  }
  if (!battle.isTurnOver) {
    return { text: battle.t('playBattle.dragHint'), muted: true }
  }
  return { text: '', muted: true }
})

function opponentSlotSrc(slot: OpponentSlot) {
  if (slot?.kind === 'card' && slot.cardId) return getCardImage(slot.cardId)
  if (slot?.kind === 'pass') return images.cards.pass
  if (slot?.kind === 'hidden') return images.cards.back
  return null
}
</script>

<template>
  <MobileFrame fullscreen>
    <LoadingOverlay :visible="battle.isBootstrapping || battle.room.isConnecting" :message="battle.t('game.syncing')" />

    <PageBackdrop
      :src="images.backgrounds.login"
      :overlay-opacity="0.7"
      class="game-battle-page flex h-screen flex-col overflow-hidden"
    >
      <div class="relative z-10 flex h-full flex-col px-3 pb-3 pt-[calc(0.5rem+var(--safe-top))]">
        <WoodPanel class="battle-board flex flex-1 flex-col">
          <header class="relative px-4 pt-3 text-center">
            <h1 class="battle-title font-display text-2xl uppercase tracking-widest">
              {{ battle.t('playBattle.title') }}
            </h1>
            <p class="text-xs font-semibold uppercase tracking-wide text-amber-100/80">
              {{ battle.t('playBattle.day', { n: battle.room.dayNumber }) }}
            </p>
            <button
              type="button"
              class="battle-help absolute right-3 top-3"
              :aria-label="battle.t('playBattle.help')"
              @click="battle.showHelp()"
            >
              <QuestionMarkCircleIcon class="h-6 w-6" />
            </button>
          </header>

          <BattlePlayerRow
            variant="enemy"
            :name="battle.opponentName"
            :avatar="battle.opponentAvatar"
            :slot-label="battle.t('playBattle.playedSlots')"
          >
            <BattleCardSlot
              v-for="(slot, i) in battle.opponentPlayedSlots"
              :key="`opp-slot-${i}`"
              :src="opponentSlotSrc(slot)"
              :flip="battle.cardsRevealed && !!slot"
            />
          </BattlePlayerRow>

          <div class="flex flex-1 flex-col justify-center gap-2">
            <div class="relative flex items-center justify-center py-2">
              <span class="battle-divider" aria-hidden="true" />
              <div class="battle-clock">{{ battle.clock }}</div>
            </div>

            <Transition name="result-pop">
              <div v-if="battle.showResultBanner" class="result-banner mx-3">
                {{ battle.resultMessage }}
              </div>
            </Transition>

            <BattlePlayerRow
              variant="me"
              :name="battle.myName"
              :avatar="battle.myAvatar"
              :slot-label="battle.t('playBattle.yourPlayedSlots')"
              :role-image="battle.myRoleImage"
              :actions-label="
                battle.isTurnOver
                  ? null
                  : battle.t('battle.actionsLeft', {
                      used: battle.remainingActions,
                      total: battle.actionsPerTurn,
                    })
              "
              :droppable="!battle.isTurnOver && !battle.holdingForReveal"
              @dragover="battle.onDragOver"
              @drop="battle.onDropPlay"
            >
              <BattleCardSlot
                v-for="(slot, i) in battle.myPlayedSlots"
                :key="`my-slot-${i}`"
                :src="slot ? getCardImage(slot) : null"
              />
            </BattlePlayerRow>
          </div>

          <BattleInventory
            :label="battle.t('playBattle.inventory')"
            :hint="battle.dayEventBattleHint || undefined"
            :empty-text="battle.t('playBattle.emptyHand')"
            :status-text="inventoryStatus.text"
            :status-muted="inventoryStatus.muted"
            :cards="battle.inventoryCards"
            :empty-slots="battle.emptyHandSlots"
            :selected-card="battle.selectedCard"
            :locked="inventoryLocked"
            @select="battle.toggleCard"
            @dragstart="battle.onDragStart"
          />
        </WoodPanel>

        <div class="mt-2 flex items-center gap-3">
          <button
            type="button"
            class="action-btn action-btn--play"
            :disabled="!battle.selectedCard || inventoryLocked"
            @click="battle.playCard()"
          >
            {{ battle.t('playBattle.playCard') }}
          </button>
        </div>
      </div>
    </PageBackdrop>
  </MobileFrame>
</template>

<style src="./GamePage.css"></style>
