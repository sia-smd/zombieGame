import { computed, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'
import { useRoomStore } from '@/stores/room.store'
import { sameUserId } from '@/utils/ids'
import { resolveLiveRoomRoute } from '@/utils/roomFlow'

export function useMatchPhaseNavigation(
  matchId: MaybeRefOrGetter<string>,
  enabled: MaybeRefOrGetter<boolean>,
  deferBattleResult: MaybeRefOrGetter<boolean> = false,
) {
  const router = useRouter()
  const route = useRoute()
  const auth = useAuthStore()
  const room = useRoomStore()

  const hasCurrentPair = computed(() => {
    const userId = auth.resolvedUserId
    return room.battlePairs.some(
      (pair) =>
        sameUserId(pair.player1Id, userId) ||
        sameUserId(pair.player2Id, userId),
    )
  })

  watch(
    [
      () => toValue(enabled),
      () => room.currentPhase,
      hasCurrentPair,
      () => toValue(deferBattleResult),
    ],
    async ([isEnabled, phase, paired, deferResult]) => {
      if (!isEnabled) return

      const id = toValue(matchId)
      const target = resolveLiveRoomRoute(phase, paired, deferResult)
      if (!target) return

      const currentId = Array.isArray(route.params.id)
        ? route.params.id[0]
        : route.params.id
      if (route.name === target && currentId === id) return

      await router.replace({ name: target, params: { id } }).catch(() => undefined)
    },
    { immediate: true },
  )

  return { hasCurrentPair }
}
