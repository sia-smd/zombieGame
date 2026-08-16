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

export type OpponentSlot = { kind: 'hidden' | 'pass' | 'card'; cardId?: string } | null

export function useBattlePlay(matchId: MaybeRefOrGetter<string>) {
  const router = useRouter()
  const room = useRoomStore()
  const auth = useAuthStore()
  const settings = useSettingsStore()
  const { t } = useI18n()

  const selectedCard = ref<string | null>(null)
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
  const isTurnOver = computed(() => remainingActions.value <= 0)

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

  const inventoryCards = computed(() =>
    (me.value?.inventoryCardIds ?? []).map((id) => {
      const sunnyBlocksPowerPoison =
        room.currentDayEvent === DayEventType.SunnyDay &&
        myRole.value === PlayerRole.PowerZombie &&
        id.toLowerCase() === ActionCardIds.poison
      return { id, image: getCardImage(id), disabled: sunnyBlocksPowerPoison }
    }),
  )

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
  const opponentFinished = computed(() => !!me.value?.opponentFinished || !!me.value?.battleFinished)
  const battleFinished = computed(
    () =>
      !!me.value?.battleFinished ||
      room.currentPhase === RoomPhase.BattleResult ||
      room.currentPhase === RoomPhase.DaySummary,
  )
  const bothTurnsDone = computed(() => isTurnOver.value && opponentFinished.value)

  const myPlayedSlots = computed(() => {
    const source = myPlayedCards.value.length ? myPlayedCards.value : serverPlayed.value
    const slots: Array<string | null> = []
    for (let i = 0; i < actionsPerTurn.value; i++) slots.push(source[i] ?? null)
    return slots
  })

  const opponentPlayedSlots = computed(() => {
    const slots: OpponentSlot[] = []
    for (let i = 0; i < actionsPerTurn.value; i++) slots.push(null)
    if (!bothTurnsDone.value && !battleFinished.value) return slots

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

  function startRevealSequence() {
    if (cardsRevealed.value || holdingForReveal.value) return
    holdingForReveal.value = true

    const id = toValue(matchId)
    if (token.value) void roomService.syncRoom(id, token.value).catch(() => undefined)

    revealTimer = setTimeout(() => {
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
    if (battleId) {
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
        clearRevealTimers()
      }
    },
  )

  watch(opponentFinished, (done) => {
    if (done && token.value) {
      void roomService.syncRoom(toValue(matchId), token.value).catch(() => undefined)
    }
  })

  watch(
    [battleFinished, bothTurnsDone],
    ([finished, bothDone]) => {
      if ((finished || bothDone) && !cardsRevealed.value) {
        startRevealSequence()
      }
    },
  )

  watch(
    () => room.currentPhase,
    (phase) => {
      if (phase === RoomPhase.BattleResult || phase === RoomPhase.DaySummary) {
        if (!cardsRevealed.value) {
          startRevealSequence()
          return
        }
        if (!holdingForReveal.value) goToBattleSummary()
      }
    },
  )

  function isCardBlocked(cardId: string) {
    return !!inventoryCards.value.find((c) => c.id === cardId)?.disabled
  }

  function toggleCard(cardId: string) {
    if (isTurnOver.value || holdingForReveal.value) return
    if (isCardBlocked(cardId)) {
      settings.pushToast('info', t('playBattle.cardDisabledSunny'))
      return
    }
    selectedCard.value = selectedCard.value === cardId ? null : cardId
  }

  function resolveTarget(cardId: string): string | undefined {
    if (isSelfTargetCard(cardId)) return undefined
    return opponentId.value ?? undefined
  }

  async function playCard(cardIdOverride?: string) {
    const cardId = cardIdOverride ?? selectedCard.value
    const pair = pairId.value
    if (!cardId || !pair || !token.value || isTurnOver.value || actionLoading.value || holdingForReveal.value)
      return
    if (isCardBlocked(cardId)) {
      settings.pushToast('info', t('playBattle.cardDisabledSunny'))
      return
    }

    actionLoading.value = true
    try {
      await roomService.playCardInBattle(
        toValue(matchId),
        token.value,
        pair,
        cardId,
        resolveTarget(cardId),
      )
      myPlayedCards.value = [...myPlayedCards.value, cardId]
      selectedCard.value = null
    } catch (e: unknown) {
      settings.reportError(e)
    } finally {
      actionLoading.value = false
    }
  }

  function onDragStart(event: DragEvent, cardId: string) {
    if (isTurnOver.value || actionLoading.value || holdingForReveal.value || isCardBlocked(cardId)) {
      event.preventDefault()
      return
    }
    event.dataTransfer?.setData('text/card-id', cardId)
    event.dataTransfer!.effectAllowed = 'move'
    selectedCard.value = cardId
  }

  function onDragOver(event: DragEvent) {
    if (isTurnOver.value || actionLoading.value || holdingForReveal.value) return
    event.preventDefault()
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move'
  }

  async function onDropPlay(event: DragEvent) {
    event.preventDefault()
    const cardId = event.dataTransfer?.getData('text/card-id')
    if (!cardId) return
    await playCard(cardId)
  }

  function showHelp() {
    settings.pushToast('info', t('playBattle.helpHint', { n: actionsPerTurn.value }))
  }

  return {
    room,
    t,
    isBootstrapping,
    selectedCard,
    actionLoading,
    cardsRevealed,
    showResultBanner,
    holdingForReveal,
    actionsPerTurn,
    remainingActions,
    isTurnOver,
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
    onDragStart,
    onDragOver,
    onDropPlay,
    showHelp,
  }
}
