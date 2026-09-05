import { test as base, type Page } from '@playwright/test'
import { RoomPhase } from '../src/types/enums'

export const PLAYER_ID = '11111111-1111-1111-1111-111111111111'
export const MATCH_ID = '22222222-2222-2222-2222-222222222222'
export const OPPONENT_ID = '33333333-3333-3333-3333-333333333333'

export function profilePayload() {
  return {
    playerId: PLAYER_ID,
    accountType: 0,
    name: 'Ada',
    username: 'ada',
    imageId: 'avatar_hunter_01',
    level: 2,
    coins: 40,
    mobileVerified: false,
    hasPassword: false,
    inventory: [],
    statistics: { wins: 1, losses: 0, matchesPlayed: 1 },
    createdDate: '2026-01-01',
  }
}

export function roomState(phase: RoomPhase) {
  const pairId = '44444444-4444-4444-4444-444444444444'
  return {
    matchId: MATCH_ID,
    currentPhase: phase,
    dayNumber: 1,
    phaseEndsAt: new Date(Date.now() + 60_000).toISOString(),
    players: [
      {
        userId: PLAYER_ID,
        username: 'Ada',
        imageId: 'avatar_hunter_01',
        isAlive: true,
        isBot: false,
        seatIndex: 0,
        isPaired: phase === RoomPhase.CardBattle,
        hasSentInvitationToday: false,
        hasPendingInvitation: false,
        isResting: false,
        isDisconnected: false,
        activity: 0,
      },
      {
        userId: OPPONENT_ID,
        username: 'Bo',
        imageId: 'avatar_default_01',
        isAlive: true,
        isBot: true,
        seatIndex: 1,
        isPaired: phase === RoomPhase.CardBattle,
        hasSentInvitationToday: false,
        hasPendingInvitation: false,
        isResting: false,
        isDisconnected: false,
        activity: 0,
      },
    ],
    battlePairs:
      phase === RoomPhase.CardBattle
        ? [
            {
              pairId,
              player1Id: PLAYER_ID,
              player2Id: OPPONENT_ID,
              status: 1,
              player1Summary: 0,
              player2Summary: 0,
            },
          ]
        : [],
    battleSummaries: [],
    availableOpponents: [OPPONENT_ID],
    pendingInvitations: [],
    availableOpponentsByPlayer: { [PLAYER_ID]: [OPPONENT_ID] },
    votesRevealed: false,
    currentDayEvent: 0,
    roomName: 'E2E Room',
    maxPlayers: 8,
    hostUserId: PLAYER_ID,
    me: {
      userId: PLAYER_ID,
      role: 1,
      roleCardId: '11111111-1111-1111-1111-111111111108',
      health: 1,
      maxHealth: 1,
      actionsPerTurn: 2,
      remainingActions: 2,
      inventoryCardIds: [],
      pairId: phase === RoomPhase.CardBattle ? pairId : null,
      opponentId: phase === RoomPhase.CardBattle ? OPPONENT_ID : null,
    },
    winTeam: phase === RoomPhase.Finished ? 1 : 0,
    snapshotVersion: 1,
    phaseSecondsRemaining: 30,
  }
}

export async function mockApi(page: Page) {
  await page.addInitScript(() => {
    sessionStorage.setItem('zvh_e2e', '1')
  })
  await page.route('**/hubs/**', (route) => route.abort())
  await page.route('**/api/**', async (route) => {
    const url = route.request().url()
    if (url.includes('/api/profile/me')) {
      await route.fulfill({ json: profilePayload() })
      return
    }
    if (url.includes('/api/account/register-guest')) {
      await route.fulfill({
        json: {
          playerId: PLAYER_ID,
          accessToken: 'must-not-persist-access',
          refreshToken: 'must-not-persist-refresh',
          accessTokenExpiresAt: new Date(Date.now() + 86400000).toISOString(),
          refreshTokenExpiresAt: new Date(Date.now() + 86400000 * 14).toISOString(),
          profile: {
            name: 'Ada',
            imageId: 'avatar_hunter_01',
            level: 1,
            coins: 10,
            wins: 0,
            losses: 0,
          },
        },
      })
      return
    }
    if (url.includes('/api/account/refresh-token')) {
      await route.fulfill({
        json: {
          accessToken: 'rotated-access',
          refreshToken: 'rotated-refresh',
          accessTokenExpiresAt: new Date(Date.now() + 86400000).toISOString(),
          refreshTokenExpiresAt: new Date(Date.now() + 86400000 * 14).toISOString(),
        },
      })
      return
    }
    if (url.includes('/api/account/logout')) {
      await route.fulfill({ json: { success: true, message: 'ok' } })
      return
    }
    if (url.includes('/api/client/boot')) {
      await route.fulfill({
        json: {
          serverVersion: '1.0.0',
          minClientVersion: '1.0.0',
          engineHealthy: true,
          databaseHealthy: true,
          redisHealthy: true,
        },
      })
      return
    }
    if (url.includes('/api/match') || url.includes('/api/game')) {
      await route.fulfill({
        json: { hasActiveMatch: false, matchId: null, sessionToken: null },
      })
      return
    }
    await route.fulfill({ status: 200, json: {} })
  })
}

export async function seedSignedIn(page: Page) {
  await page.addInitScript(
    ({ playerId, matchId }) => {
      localStorage.setItem('zvh_lang', 'en')
      localStorage.setItem('zvh_auth', '1')
      localStorage.setItem('zvh_player_id', playerId)
      localStorage.setItem(
        'zvh_match_session',
        JSON.stringify({ matchId, token: 'e2e-room-token' }),
      )
    },
    { playerId: PLAYER_ID, matchId: MATCH_ID },
  )
}

export async function seedE2eRoom(page: Page, phase: RoomPhase) {
  const state = roomState(phase)
  await page.evaluate((next) => {
    sessionStorage.setItem('zvh_e2e', '1')
    sessionStorage.setItem('zvh_e2e_state', JSON.stringify(next))
  }, state)
}

export const test = base
export { expect } from '@playwright/test'
