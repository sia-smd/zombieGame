import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { RoomStateDto } from '@/types/api'
import { roomService } from '@/services/room.service'
import { useRoomStore } from './room.store'

vi.mock('@/services/room.service', () => {
  const unsubscribe = () => undefined
  return {
    roomService: {
      joinRoom: vi.fn(),
      resumeMatch: vi.fn(),
      syncRoom: vi.fn(),
      onRoomUpdated: vi.fn(() => unsubscribe),
      onPhaseChanged: vi.fn(() => unsubscribe),
      onGameStarted: vi.fn(() => unsubscribe),
      onPlayerJoined: vi.fn(() => unsubscribe),
      onBattleState: vi.fn(() => unsubscribe),
      onChatMessage: vi.fn(() => unsubscribe),
      onVoteStarted: vi.fn(() => unsubscribe),
      onVoteFinished: vi.fn(() => unsubscribe),
      onPlayerEliminated: vi.fn(() => unsubscribe),
      onDayStarted: vi.fn(() => unsubscribe),
      onGameFinished: vi.fn(() => unsubscribe),
      onInvitationSent: vi.fn(() => unsubscribe),
      onInvitationAccepted: vi.fn(() => unsubscribe),
      onInvitationsCancelled: vi.fn(() => unsubscribe),
      onBattleStarted: vi.fn(() => unsubscribe),
      onBattleFinished: vi.fn(() => unsubscribe),
      onVoteUpdated: vi.fn(() => unsubscribe),
      onMatchResumed: vi.fn(() => unsubscribe),
      onDiscussionStarted: vi.fn(() => unsubscribe),
    },
  }
})

function roomState(matchId: string): RoomStateDto {
  return {
    matchId,
    currentPhase: 0,
    dayNumber: 1,
    players: [],
    battlePairs: [],
    battleSummaries: [],
    pendingInvitations: [],
  } as unknown as RoomStateDto
}

describe('room store synchronization', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(roomService.resumeMatch).mockReset().mockResolvedValue(undefined)
    vi.mocked(roomService.syncRoom).mockReset().mockResolvedValue(undefined)
  })

  it('propagates sync failures and records the latest message', async () => {
    const room = useRoomStore()
    room.matchId = 'match-a'
    room.sessionToken = 'token-a'
    vi.mocked(roomService.syncRoom).mockRejectedValueOnce(new Error('sync failed'))

    await expect(room.syncNow()).rejects.toThrow('sync failed')
    expect(room.lastSyncError).toBe('sync failed')
  })

  it('recovers once with ResumeMatch followed by SyncRoom', async () => {
    const room = useRoomStore()
    room.matchId = 'match-a'
    room.sessionToken = 'token-a'
    room.state = roomState('match-a')
    const order: string[] = []
    vi.mocked(roomService.resumeMatch).mockImplementation(async () => {
      order.push('resume')
    })
    vi.mocked(roomService.syncRoom).mockImplementation(async () => {
      order.push('sync')
    })

    await Promise.all([room.recoverConnection(), room.recoverConnection()])

    expect(order).toEqual(['resume', 'sync'])
    expect(roomService.resumeMatch).toHaveBeenCalledTimes(1)
    expect(roomService.syncRoom).toHaveBeenCalledTimes(1)
  })

  it('clears active and diagnostic state on reset', () => {
    const room = useRoomStore()
    room.matchId = 'match-a'
    room.sessionToken = 'token-a'
    room.state = roomState('match-a')

    room.reset()

    expect(room.matchId).toBeNull()
    expect(room.sessionToken).toBeNull()
    expect(room.state).toBeNull()
    expect(room.lastSyncError).toBeNull()
  })

  it('ignores an older snapshot for the same match', () => {
    const room = useRoomStore()
    room.applyState({ ...roomState('match-a'), snapshotVersion: 4, dayNumber: 2 })
    room.applyState({ ...roomState('match-a'), snapshotVersion: 3, dayNumber: 1 })

    expect(room.state?.snapshotVersion).toBe(4)
    expect(room.state?.dayNumber).toBe(2)
  })

  it('applies a newer snapshot for the same match', () => {
    const room = useRoomStore()
    room.applyState({ ...roomState('match-a'), snapshotVersion: 4, dayNumber: 2 })
    room.applyState({ ...roomState('match-a'), snapshotVersion: 5, dayNumber: 3 })

    expect(room.state?.snapshotVersion).toBe(5)
    expect(room.state?.dayNumber).toBe(3)
  })

  it('ignores private me that belongs to another player', () => {
    localStorage.setItem('zvh_player_id', 'viewer-1')
    const room = useRoomStore()
    room.applyState({
      ...roomState('match-a'),
      me: {
        userId: 'viewer-1',
        role: 0,
        roleCardId: 'role-human',
        health: 1,
        maxHealth: 1,
        actionsPerTurn: 2,
        remainingActions: 2,
        inventoryCardIds: [],
      },
    })
    room.applyState({
      ...roomState('match-a'),
      snapshotVersion: 2,
      me: {
        userId: 'attacker-2',
        role: 1,
        roleCardId: 'role-zombie',
        health: 1,
        maxHealth: 1,
        actionsPerTurn: 2,
        remainingActions: 1,
        inventoryCardIds: [],
      },
    })

    expect(room.myBattle?.userId).toBe('viewer-1')
    expect(room.myBattle?.role).toBe(0)
  })
})
