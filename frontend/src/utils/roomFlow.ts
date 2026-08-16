import type { ActiveMatchResponse } from '@/types/api'
import { RoomPhase } from '@/types/enums'

export type RoomFlowRoute =
  | 'lobby'
  | 'game'
  | 'battle'
  | 'battle-summary'
  | 'voting'
  | 'match-result'

export function parseRoomPhase(raw: unknown): RoomPhase | null {
  if (typeof raw === 'number' && Number.isInteger(raw) && RoomPhase[raw] !== undefined) {
    return raw
  }

  if (typeof raw === 'string') {
    const value = raw.trim()
    const byName = RoomPhase[value as keyof typeof RoomPhase]
    if (typeof byName === 'number') return byName

    const enumName = Object.keys(RoomPhase).find(
      (key) => Number.isNaN(Number(key)) && key.toLowerCase() === value.toLowerCase(),
    )
    if (enumName) return RoomPhase[enumName as keyof typeof RoomPhase] as RoomPhase

    if (value !== '') {
      const asNumber = Number(value)
      if (
        Number.isInteger(asNumber) &&
        RoomPhase[asNumber] !== undefined
      ) {
        return asNumber
      }
    }
  }

  return null
}

export function resolveActiveMatchRoute(active: ActiveMatchResponse): RoomFlowRoute {
  const phase = parseRoomPhase(active.phase)

  if (phase === RoomPhase.Lobby) return 'lobby'
  if (phase === RoomPhase.CardBattle) return active.battleId ? 'battle' : 'game'
  if (phase === RoomPhase.BattleResult || phase === RoomPhase.DaySummary) {
    return 'battle-summary'
  }
  if (phase === RoomPhase.Voting || phase === RoomPhase.VoteResult) return 'voting'
  if (phase === RoomPhase.Finished) return 'match-result'
  return 'game'
}

export function resolveLiveRoomRoute(
  phase: unknown,
  hasCurrentPair: boolean,
  deferBattleResult = false,
): RoomFlowRoute | null {
  const parsed = parseRoomPhase(phase)

  if (parsed === RoomPhase.Lobby) return 'lobby'
  if (parsed === RoomPhase.CardBattle) return hasCurrentPair ? 'battle' : 'game'
  if (parsed === RoomPhase.BattleResult || parsed === RoomPhase.DaySummary) {
    return deferBattleResult ? null : 'battle-summary'
  }
  if (parsed === RoomPhase.Voting || parsed === RoomPhase.VoteResult) return 'voting'
  if (parsed === RoomPhase.Finished) return 'match-result'
  return 'game'
}
