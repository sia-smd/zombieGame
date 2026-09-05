import type { RoomStateDto } from '@/types/api'

const E2E_FLAG = 'zvh_e2e'
const E2E_STATE = 'zvh_e2e_state'

/** Playwright sets sessionStorage.zvh_e2e; production play never does. */
export function isE2eHarness(): boolean {
  return typeof sessionStorage !== 'undefined' && sessionStorage.getItem(E2E_FLAG) === '1'
}

type RoomSeed = {
  applyState: (state: RoomStateDto) => void
  sessionToken: string | null
}

/** Playwright-only room seed. Production play never sets sessionStorage.zvh_e2e. */
export function tryApplyE2eRoomState(
  room: RoomSeed,
  matchId: string,
  token: string | null,
): boolean {
  if (typeof sessionStorage === 'undefined') return false
  if (sessionStorage.getItem(E2E_FLAG) !== '1') return false

  const raw = sessionStorage.getItem(E2E_STATE)
  if (!raw) return false

  try {
    const state = JSON.parse(raw) as RoomStateDto
    if (!state?.matchId || state.matchId.toLowerCase() !== matchId.trim().toLowerCase()) {
      return false
    }
    room.sessionToken = token || 'e2e-token'
    room.applyState(state)
    return true
  } catch {
    return false
  }
}
