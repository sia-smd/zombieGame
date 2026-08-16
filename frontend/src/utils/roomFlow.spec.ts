import { describe, expect, it } from 'vitest'
import type { ActiveMatchResponse } from '@/types/api'
import { RoomPhase } from '@/types/enums'
import {
  parseRoomPhase,
  resolveActiveMatchRoute,
  resolveLiveRoomRoute,
} from './roomFlow'

function active(phase: RoomPhase, battleId: string | null = null): ActiveMatchResponse {
  return {
    hasActiveMatch: true,
    matchId: 'match-a',
    day: 1,
    phase: RoomPhase[phase],
    battleId,
    alive: true,
    role: 'Human',
    playersAlive: 4,
    sessionToken: 'token-a',
  }
}

describe('room phase routing', () => {
  it.each([
    [RoomPhase.Lobby, false, 'lobby'],
    [RoomPhase.DayStart, false, 'game'],
    [RoomPhase.OpponentSelection, false, 'game'],
    [RoomPhase.BattlePreparation, false, 'game'],
    [RoomPhase.CardBattle, false, 'game'],
    [RoomPhase.CardBattle, true, 'battle'],
    [RoomPhase.BattleResult, false, 'battle-summary'],
    [RoomPhase.DaySummary, false, 'battle-summary'],
    [RoomPhase.Discussion, false, 'game'],
    [RoomPhase.Voting, false, 'voting'],
    [RoomPhase.VoteResult, false, 'voting'],
    [RoomPhase.Finished, false, 'match-result'],
  ] as const)('routes phase %s with paired=%s to %s', (phase, paired, expected) => {
    expect(resolveLiveRoomRoute(phase, paired)).toBe(expected)
  })

  it('defers battle result navigation while reveal animation owns the flow', () => {
    expect(resolveLiveRoomRoute(RoomPhase.BattleResult, false, true)).toBeNull()
  })

  it('uses the active battle id on cold resume', () => {
    expect(resolveActiveMatchRoute(active(RoomPhase.CardBattle, 'battle-a'))).toBe('battle')
    expect(resolveActiveMatchRoute(active(RoomPhase.CardBattle))).toBe('game')
  })

  it('parses enum names and serialized numeric phases safely', () => {
    expect(parseRoomPhase('discussion')).toBe(RoomPhase.Discussion)
    expect(parseRoomPhase(String(RoomPhase.Voting))).toBe(RoomPhase.Voting)
    expect(parseRoomPhase('unknown')).toBeNull()
  })
})
