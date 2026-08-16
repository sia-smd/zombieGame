import { computed, onScopeDispose, ref, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'
import { useRoomStore } from '@/stores/room.store'
import { useSettingsStore } from '@/stores/settings.store'
import { translate } from '@/i18n'

export type RoomSessionMode = 'join' | 'resume'

export function useRoomSession(
  matchId: MaybeRefOrGetter<string>,
  mode: RoomSessionMode,
) {
  const router = useRouter()
  const auth = useAuthStore()
  const room = useRoomStore()
  const settings = useSettingsStore()

  const ready = ref(false)
  const isBootstrapping = ref(false)
  function roomTokenFor(id: string): string | null {
    return room.matchId?.toLowerCase() === id.trim().toLowerCase()
      ? room.sessionToken
      : null
  }

  const sessionToken = computed(() => {
    const id = toValue(matchId)
    return auth.getMatchSession(id)?.token ?? roomTokenFor(id) ?? ''
  })

  let disposed = false
  let bootstrapPromise: Promise<boolean> | null = null
  let bootstrappedMatchId: string | null = null
  let generation = 0

  watch(
    () => toValue(matchId),
    (next, previous) => {
      if (next === previous) return
      generation++
      ready.value = false
      bootstrappedMatchId = null
      bootstrapPromise = null
      if (!disposed) void bootstrap()
    },
  )

  onScopeDispose(() => {
    disposed = true
  })

  async function bootstrap(): Promise<boolean> {
    const requestedId = toValue(matchId)
    if (ready.value && bootstrappedMatchId === requestedId) return true
    if (bootstrapPromise) return bootstrapPromise
    const runGeneration = generation

    const run = (async () => {
      isBootstrapping.value = true

      try {
        auth.hydrateFromStorage()

        const id = requestedId
        const token = auth.getMatchSession(id)?.token ?? roomTokenFor(id)
        if (!token) {
          if (!disposed) await router.replace({ name: 'home' })
          return false
        }

        const hasSameRoomState =
          room.matchId === id &&
          room.state?.matchId === id

        if (hasSameRoomState) {
          await room.syncNow()
        } else {
          if (mode === 'join') {
            await room.connect(id, token)
          } else {
            await room.resume(id, token)
          }
          await room.syncNow()
        }

        if (room.state?.matchId !== id) {
          throw new Error(translate('errors.roomRestoreFailed'))
        }
        if (
          disposed ||
          generation !== runGeneration ||
          toValue(matchId) !== id
        ) {
          return false
        }
        bootstrappedMatchId = id
        ready.value = true
        return true
      } catch (error: unknown) {
        if (
          !disposed &&
          generation === runGeneration &&
          toValue(matchId) === requestedId
        ) {
          const normalized = settings.reportError(
            error,
            translate('errors.roomRestoreFailed'),
          )
          if (normalized.kind !== 'auth') {
            await router.replace({ name: 'home' })
          }
        }
        return false
      } finally {
        if (!disposed && generation === runGeneration) {
          isBootstrapping.value = false
        }
      }
    })()

    bootstrapPromise = run
    try {
      return await run
    } finally {
      if (bootstrapPromise === run) bootstrapPromise = null
    }
  }

  return {
    ready,
    isBootstrapping,
    sessionToken,
    bootstrap,
  }
}
