import { describe, expect, it, vi } from 'vitest'
import { RoomPhase } from '@/types/enums'
import { resolveLiveRoomRoute } from '@/utils/roomFlow'
import { createConnectionRecovery, type RecoverableRoom } from './connection-recovery'

function room(overrides: Partial<RecoverableRoom> = {}): RecoverableRoom {
  return {
    matchId: 'match-a',
    sessionToken: 'token-a',
    recoverConnection: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  }
}

describe('connection recovery coordinator', () => {
  it('reconnects the room transport and restores the room session on retry', async () => {
    const activeRoom = room()
    const connect = vi.fn().mockResolvedValue(undefined)
    const recovery = createConnectionRecovery({
      room: activeRoom,
      connect,
      closeConnectionAlert: vi.fn(),
      showConnectionAlert: vi.fn(),
    })

    await recovery.retry('game')

    expect(connect).toHaveBeenCalledWith('room')
    expect(activeRoom.recoverConnection).toHaveBeenCalledTimes(1)
  })

  it('closes the alert only after room recovery completes', async () => {
    let finishRecovery!: () => void
    const activeRoom = room({
      recoverConnection: vi.fn(
        () => new Promise<void>((resolve) => {
          finishRecovery = resolve
        }),
      ),
    })
    const close = vi.fn()
    const recovery = createConnectionRecovery({
      room: activeRoom,
      closeConnectionAlert: close,
      showConnectionAlert: vi.fn(),
    })

    const pending = recovery.afterReconnect('room')
    expect(close).not.toHaveBeenCalled()

    finishRecovery()
    await pending
    expect(close).toHaveBeenCalledTimes(1)
  })

  it('keeps the alert open and provides retry when recovery fails', async () => {
    const activeRoom = room({
      recoverConnection: vi.fn().mockRejectedValue(new Error('resume failed')),
    })
    const close = vi.fn()
    const show = vi.fn()
    const recovery = createConnectionRecovery({
      room: activeRoom,
      closeConnectionAlert: close,
      showConnectionAlert: show,
    })

    await recovery.afterReconnect('room')

    expect(close).not.toHaveBeenCalled()
    expect(show).toHaveBeenCalledOnce()
    expect(show.mock.calls[0]?.[0]).toBeTypeOf('function')
  })

  it('does not recover a room when no active match exists', async () => {
    const inactiveRoom = room({ matchId: null, sessionToken: null })
    const close = vi.fn()
    const recovery = createConnectionRecovery({
      room: inactiveRoom,
      closeConnectionAlert: close,
      showConnectionAlert: vi.fn(),
    })

    await recovery.afterReconnect('game')

    expect(inactiveRoom.recoverConnection).not.toHaveBeenCalled()
    expect(close).toHaveBeenCalledTimes(1)
  })

  it('after reconnect, live navigation still follows recovered phase', async () => {
    const activeRoom = room()
    const recovery = createConnectionRecovery({
      room: activeRoom,
      connect: vi.fn().mockResolvedValue(undefined),
      closeConnectionAlert: vi.fn(),
      showConnectionAlert: vi.fn(),
    })

    await recovery.retry('game')

    expect(activeRoom.recoverConnection).toHaveBeenCalledTimes(1)
    expect(resolveLiveRoomRoute(RoomPhase.Voting, false)).toBe('voting')
    expect(resolveLiveRoomRoute(RoomPhase.Finished, false)).toBe('match-result')
    expect(resolveLiveRoomRoute(RoomPhase.CardBattle, true)).toBe('battle')
  })
})
