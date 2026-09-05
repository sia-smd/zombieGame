import { computed, onMounted, onUnmounted, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '@/stores/auth.store'
import { useRoomStore } from '@/stores/room.store'
import { useSettingsStore } from '@/stores/settings.store'
import { useMatchPhaseNavigation } from '@/composables/useMatchPhaseNavigation'
import { useRoomSession } from '@/composables/useRoomSession'
import { useCountdown } from '@/composables/useAnimation'
import { BattlePublicAction, DayEventType, PlayerRole, RoomPhase } from '@/types/enums'
import { ActionCardIds, getCardImage, getRoleImage, isSelfTargetCard } from '@/utils/imageAssets'
import { resolveAvatarUrl } from '@/utils/avatarAssets'
import { playerAvatarUrl } from '@/utils/playerAvatar'
import { sameUserId } from '@/utils/ids'
import { roomService } from '@/services/room.service'
import { gameAudio } from '@/services/game-audio'
import { isE2eHarness } from '@/utils/e2eRoom'

export type InventoryCard = {
  slotIndex: number
  id: string
  image: string
  disabled: boolean
}

export type OpponentSlot = { kind: 'hidden' | 'pass' | 'card'; cardId?: string } | null

export function useBattlePlay(matchId: MaybeRefOrGetter<string>) {
  const router = useRouter()
  const room = useRoomStore()
  const auth = useAuthStore()
  const settings = useSettingsStore()
  const { t } = useI18n()

  const selectedSlot = ref<number | null>(null)
  const actionLoading = ref(false)
  const myPlayedCards = ref<string[]>([])
  const cardsRevealed = ref(false)
  const showResultBanner = ref(false)
  const holdingForReveal = ref(false)
  let revealTimer: ReturnType<typeof setTimeout> | null = null
  let leaveTimer: ReturnType<typeof setTimeout> | null = null

  const myId = computed(() => auth.resolvedUserId)
  const { ready, sessionToken: token, isBootstrapping, bootstrap } = useRoomSession(matchId, 'resume')
  const deferBattleResult = computed(() => !cardsRevealed.value || holdingForReveal.value)
  useMatchPhaseNavigation(matchId, ready, deferBattleResult)

  const me = computed(() => room.myBattle)
  const myRole = computed<PlayerRole>(() => me.value?.role ?? PlayerRole.Unknown)
  const myName = computed(
    () =>
      room.players.find((p) => sameUserId(p.userId, myId.value))?.username ??
      auth.profile?.username ??
      t('playBattle.you'),
  )

  const actionsPerTurn = computed(() => me.value?.actionsPerTurn ?? 2)
  const remainingActions = computed(() => me.value?.remainingActions ?? actionsPerTurn.value)
  const myTurnFinished = computed(() => !!me.value?.myTurnFinished)
  const isTurnOver = computed(() => myTurnFinished.value)
  const canPlayMoreCards = computed(
    () => !isTurnOver.value && !holdingForReveal.value && remainingActions.value > 0,
  )
  const needsFinish = computed(() => !isTurnOver.value && !holdingForReveal.value && remainingActions.value <= 0)

  const myPair = computed(
    () =>
      room.battlePairs.find(
        (p) => sameUserId(p.player1Id, myId.value) || sameUserId(p.player2Id, myId.value),
      ) ?? null,
  )
  const pairId = computed(() => myPair.value?.pairId ?? me.value?.pairId ?? null)

  const opponentId = computed(() => {
    if (me.value?.opponentId) return me.value.opponentId
    const pair = myPair.value
    if (!pair) return null
    return sameUserId(pair.player1Id, myId.value) ? pair.player2Id : pair.player1Id
  })

  const opponentPlayer = computed(
    () => room.players.find((p) => sameUserId(p.userId, opponentId.value)) ?? null,
  )
  const opponentName = computed(() => opponentPlayer.value?.username ?? t('playBattle.opponent'))

  const opponentAction = computed<BattlePublicAction>(() => {
    const pair = myPair.value
    if (!pair) return BattlePublicAction.None
    return (
      sameUserId(pair.player1Id, myId.value) ? pair.player2Summary : pair.player1Summary
    ) as BattlePublicAction
  })

  const inventoryCards = computed<InventoryCard[]>(() => {
    const slots = me.value?.inventorySlots
    if (slots?.length) {
      return slots.flatMap((id, slotIndex) => {
        if (!id) return []
        const sunnyBlocksPowerPoison =
          room.currentDayEvent === DayEventType.SunnyDay &&
          myRole.value === PlayerRole.PowerZombie &&
          id.toLowerCase() === ActionCardIds.poison
        return [{ slotIndex, id, image: getCardImage(id), disabled: sunnyBlocksPowerPoison }]
      })
    }

    return (me.value?.inventoryCardIds ?? []).map((id, slotIndex) => {
      const sunnyBlocksPowerPoison =
        room.currentDayEvent === DayEventType.SunnyDay &&
        myRole.value === PlayerRole.PowerZombie &&
        id.toLowerCase() === ActionCardIds.poison
      return { slotIndex, id, image: getCardImage(id), disabled: sunnyBlocksPowerPoison }
    })
  })

  const selectedCard = computed(() => {
    if (selectedSlot.value === null) return null
    return inventoryCards.value.find((c) => c.slotIndex === selectedSlot.value)?.id ?? null
  })

  const emptyHandSlots = computed(() => Math.max(0, 4 - inventoryCards.value.length))

  const dayEventBattleHint = computed(() => {
    if (room.currentDayEvent === DayEventType.SunnyDay && myRole.value === PlayerRole.PowerZombie) {
      return t('playBattle.eventHintSunny')
    }
    if (room.currentDayEvent === DayEventType.Storm) {
      return t('playBattle.eventHintStorm')
    }
    return ''
  })

  const myRoleImage = computed(() => getRoleImage(myRole.value))
  const myAvatar = computed(() => resolveAvatarUrl(auth.profile) ?? playerAvatarUrl())
  const opponentAvatar = computed(() => playerAvatarUrl(opponentPlayer.value?.imageId))

  const { clock } = useCountdown(
    () => room.state?.phaseEndsAt,
    true,
    () => room.state?.phaseSecondsRemaining,
    () => room.snapshotReceivedAt,
  )

  const serverPlayed = computed(() => me.value?.playedCardIds ?? [])
  const opponentRevealedCards = computed(() => me.value?.opponentPlayedCardIds ?? [])
  const opponentFinished = computed(() => !!me.value?.opponentFinished)
  const battleFinished = computed(
    () =>
      !!me.value?.battleFinished ||
      room.currentPhase === RoomPhase.BattleResult ||
      room.currentPhase === RoomPhase.DaySummary,
  )
  const bothTurnsDone = computed(() => myTurnFinished.value && opponentFinished.value)

  const myPlayedSlots = computed(() => {
    const source = myPlayedCards.value.length ? myPlayedCards.value : serverPlayed.value
    const slots: Array<string | null> = []
    for (let i = 0; i < actionsPerTurn.value; i++) slots.push(source[i] ?? null)
    return slots
  })

  const opponentPlayedSlots = computed(() => {
    const slots: OpponentSlot[] = []
    for (let i = 0; i < actionsPerTurn.value; i++) slots.push(null)

    if (!bothTurnsDone.value && !battleFinished.value) {
      if (opponentFinished.value) {
        const count = Math.max(1, opponentRevealedCards.value.length || (opponentAction.value === BattlePublicAction.Pass ? 1 : 0))
        for (let i = 0; i < Math.min(count, slots.length); i++) slots[i] = { kind: 'hidden' }
      }
      return slots
    }

    if (cardsRevealed.value && opponentRevealedCards.value.length) {
      opponentRevealedCards.value.forEach((id, i) => {
        if (i < slots.length) slots[i] = { kind: 'card', cardId: id }
      })
      return slots
    }

    const count = Math.max(
      1,
      opponentRevealedCards.value.length || (opponentAction.value === BattlePublicAction.Pass ? 1 : 0),
    )
    for (let i = 0; i < Math.min(count, slots.length); i++) {
      slots[i] = { kind: 'hidden' }
    }
    return slots
  })

  const resultMessage = computed(() => {
    const meAlive = room.players.find((p) => sameUserId(p.userId, myId.value))?.isAlive !== false
    const oppAlive = opponentPlayer.value?.isAlive !== false

    if (!meAlive && !oppAlive) return t('playBattle.resultBothDown')
    if (!meAlive) return t('playBattle.resultYouEliminated')
    if (!oppAlive) return t('playBattle.resultOpponentEliminated', { name: opponentName.value })
    return t('playBattle.resultStandoff', { name: opponentName.value })
  })

  function clearRevealTimers() {
    if (revealTimer) {
      clearTimeout(revealTimer)
      revealTimer = null
    }
    if (leaveTimer) {
      clearTimeout(leaveTimer)
      leaveTimer = null
    }
  }

  function goToBattleSummary() {
    holdingForReveal.value = false
    router.replace({ name: 'battle-summary', params: { id: toValue(matchId) } })
  }

  async function startRevealSequence() {
    if (cardsRevealed.value || holdingForReveal.value) return
    holdingForReveal.value = true

    const id = toValue(matchId)
    if (token.value) {
      try {
        await roomService.syncRoom(id, token.value)
      } catch {
        // reveal still proceeds; a later sync may fill opponent cards
      }
    }

    if (opponentRevealedCards.value.length === 0 && token.value) {
      await new Promise((resolve) => setTimeout(resolve, 300))
    }

    revealTimer = setTimeout(() => {
      gameAudio.playSfx('showResultPlayCard')
      cardsRevealed.value = true
      showResultBanner.value = true
      leaveTimer = setTimeout(() => {
        if (
          room.currentPhase === RoomPhase.BattleResult ||
          room.currentPhase === RoomPhase.DaySummary
        ) {
          goToBattleSummary()
        } else {
          holdingForReveal.value = false
        }
      }, 3200)
    }, 900)
  }

  onMounted(async () => {
    if (!(await bootstrap())) return
    const session = token.value
    if (!session) return
    const id = toValue(matchId)

    const battleId = pairId.value
    if (battleId && !isE2eHarness()) {
      try {
        await roomService.joinBattle(id, session, battleId)
      } catch {
        await roomService.syncRoom(id, session).catch(() => undefined)
        const retryId = pairId.value
        if (retryId && retryId !== battleId) {
          await roomService.joinBattle(id, session, retryId).catch(() => undefined)
        }
      }
    }
  })

  onUnmounted(() => {
    clearRevealTimers()
  })

  watch(
    [() => myPair.value?.pairId, remainingActions],
    ([, remaining], [prevPair]) => {
      if (myPair.value?.pairId !== prevPair || remaining === actionsPerTurn.value) {
        myPlayedCards.value = []
        cardsRevealed.value = false
        showResultBanner.value = false
        holdingForReveal.value = false
        selectedSlot.value = null
        clearRevealTimers()
      }
    },
  )

  watch(
    [battleFinished, bothTurnsDone],
    ([finished, bothDone]) => {
      if ((finished || bothDone) && !cardsRevealed.value) {
        void startRevealSequence()
      }
    },
  )

  watch(
    () => room.currentPhase,
    (phase) => {
      if (phase === RoomPhase.BattleResult || phase === RoomPhase.DaySummary) {
        if (!cardsRevealed.value) {
          void startRevealSequence()
          return
        }
        if (!holdingForReveal.value) goToBattleSummary()
      }
    },
  )

  watch(opponentRevealedCards, (cards) => {
    if (holdingForReveal.value && !cardsRevealed.value && cards.length > 0 && bothTurnsDone.value) {
      cardsRevealed.value = true
      gameAudio.playSfx('showResultPlayCard')
    }
  })

  function isCardBlocked(card: InventoryCard) {
    return card.disabled
  }

  function toggleCard(slotIndex: number) {
    if (isTurnOver.value || holdingForReveal.value) return
    const card = inventoryCards.value.find((c) => c.slotIndex === slotIndex)
    if (!card) return
    if (isCardBlocked(card)) {
      settings.pushToast('info', t('playBattle.cardDisabledSunny'))
      return
    }
    selectedSlot.value = selectedSlot.value === slotIndex ? null : slotIndex
  }

  function resolveTarget(cardId: string): string | undefined {
    if (isSelfTargetCard(cardId)) return undefined
    return opponentId.value ?? undefined
  }

  async function playCard(slotIndexOverride?: number) {
    const slotIndex = slotIndexOverride ?? selectedSlot.value
    const card = inventoryCards.value.find((c) => c.slotIndex === slotIndex)
    const pair = pairId.value
    if (
      !card ||
      !pair ||
      !token.value ||
      isTurnOver.value ||
      actionLoading.value ||
      holdingForReveal.value ||
      remainingActions.value <= 0
    )
      return
    if (isCardBlocked(card)) {
      settings.pushToast('info', t('playBattle.cardDisabledSunny'))
      return
    }

    actionLoading.value = true
    try {
      await roomService.playCardInBattle(
        toValue(matchId),
        token.value,
        pair,
        card.id,
        resolveTarget(card.id),
        card.slotIndex,
      )
      myPlayedCards.value = [...myPlayedCards.value, card.id]
      selectedSlot.value = null
    } catch (e: unknown) {
      settings.reportError(e)
    } finally {
      actionLoading.value = false
    }
  }

  async function finishTurn() {
    const pair = pairId.value
    if (!pair || !token.value || isTurnOver.value || actionLoading.value || holdingForReveal.value) return

    actionLoading.value = true
    try {
      await roomService.finishBattleTurn(toValue(matchId), token.value, pair)
    } catch (e: unknown) {
      settings.reportError(e)
    } finally {
      actionLoading.value = false
    }
  }

  function onDragStart(event: DragEvent, slotIndex: number) {
    const card = inventoryCards.value.find((c) => c.slotIndex === slotIndex)
    if (
      !card ||
      isTurnOver.value ||
      actionLoading.value ||
      holdingForReveal.value ||
      remainingActions.value <= 0 ||
      isCardBlocked(card)
    ) {
      event.preventDefault()
      return
    }
    event.dataTransfer?.setData('text/card-slot', String(slotIndex))
    event.dataTransfer!.effectAllowed = 'move'
    selectedSlot.value = slotIndex
  }

  function onDragOver(event: DragEvent) {
    if (isTurnOver.value || actionLoading.value || holdingForReveal.value) return
    event.preventDefault()
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  }

  async function onDropPlay(event: DragEvent) {
    event.preventDefault()
    const raw = event.dataTransfer?.getData('text/card-slot')
    if (!raw) return
    const slotIndex = Number.parseInt(raw, 10)
    if (Number.isNaN(slotIndex)) return
    await playCard(slotIndex)
  }

  function showHelp() {
    settings.pushToast('info', t('playBattle.helpHint', { n: actionsPerTurn.value }))
  }

  return {
    room,
    t,
    isBootstrapping,
    selectedSlot,
    selectedCard,
    actionLoading,
    cardsRevealed,
    showResultBanner,
    holdingForReveal,
    actionsPerTurn,
    remainingActions,
    myTurnFinished,
    isTurnOver,
    canPlayMoreCards,
    needsFinish,
    inventoryCards,
    emptyHandSlots,
    dayEventBattleHint,
    myRoleImage,
    myAvatar,
    opponentAvatar,
    myName,
    opponentName,
    clock,
    myPlayedSlots,
    opponentPlayedSlots,
    resultMessage,
    toggleCard,
    playCard,
    finishTurn,
    onDragStart,
    onDragOver,
    onDropPlay,
    showHelp,
  }
}
