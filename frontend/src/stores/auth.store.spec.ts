import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { disconnectAllHubs } from '@/services/signalr'
import { authSession } from '@/services/api'
import { profileService } from '@/services/profile.service'
import { useAuthStore } from './auth.store'
import { useRoomStore } from './room.store'
import { useGameStore } from './game.store'

vi.mock('@/services/signalr', () => ({
  disconnectAllHubs: vi.fn().mockResolvedValue(undefined),
  onHubEvent: vi.fn(() => () => undefined),
  connectHub: vi.fn(),
  invokeHub: vi.fn(),
}))

vi.mock('@/services/auth.service', () => ({
  authService: {
    registerGuest: vi.fn(),
    login: vi.fn(),
    sendRecoverAccountOtp: vi.fn(),
    verifyRecoverAccountOtp: vi.fn(),
    logout: vi.fn().mockResolvedValue(undefined),
  },
}))

vi.mock('@/services/profile.service', () => ({
  profileService: {
    getProfile: vi.fn(),
  },
}))

describe('auth store session lifecycle', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('binds the room token to its match id', () => {
    const auth = useAuthStore()

    auth.setMatchSession('match-a', 'token-a')

    expect(auth.getMatchSession('match-a')?.token).toBe('token-a')
    expect(auth.getMatchSession('match-b')).toBeNull()
  })

  it('marks signed-in without storing JWTs', () => {
    localStorage.setItem('zvh_refresh_token', 'old-refresh')
    localStorage.setItem('zvh_access_token', 'old-access')
    const auth = useAuthStore()

    auth.setTokens('new-access', 'new-refresh')

    expect(auth.isAuthenticated).toBe(true)
    expect(authSession.isSignedIn()).toBe(true)
    expect(localStorage.getItem('zvh_access_token')).toBeNull()
    expect(localStorage.getItem('zvh_refresh_token')).toBeNull()
  })

  it('clears auth, room, game and persisted match state together', async () => {
    const auth = useAuthStore()
    const room = useRoomStore()
    const game = useGameStore()
    auth.setUserId('player-1')
    auth.setTokens()
    auth.setMatchSession('match-a', 'token-a')
    room.matchId = 'match-a'
    game.isInQueue = true

    await auth.clearClientSession()

    expect(disconnectAllHubs).toHaveBeenCalledTimes(1)
    expect(auth.isAuthenticated).toBe(false)
    expect(authSession.isSignedIn()).toBe(false)
    expect(auth.matchSession).toBeNull()
    expect(room.matchId).toBeNull()
    expect(game.isInQueue).toBe(false)
    expect(localStorage.getItem('zvh_player_id')).toBeNull()
  })

  it('loads server profile when a signed-in session already exists', async () => {
    vi.mocked(profileService.getProfile).mockResolvedValue({
      playerId: 'player-1',
      accountType: 0,
      name: 'Ada',
      username: 'ada',
      imageId: 'avatar_hunter_01',
      level: 2,
      coins: 175,
      mobileVerified: false,
      hasPassword: false,
      inventory: [],
      statistics: { wins: 3, losses: 1, matchesPlayed: 4 },
      createdDate: '2026-01-01',
    })

    const auth = useAuthStore()
    auth.setTokens()

    await auth.initGuest()

    expect(profileService.getProfile).toHaveBeenCalled()
    expect(auth.coinCount).toBe(175)
    expect(auth.displayName).toBe('Ada')
  })
})
