export type MatchSession = {
  matchId: string
  token: string
}

const STORAGE_MATCH_SESSION = 'zvh_match_session'
const LEGACY_SESSION_TOKEN = 'zvh_session_token'

function isMatchSession(value: unknown): value is MatchSession {
  if (!value || typeof value !== 'object') return false
  const candidate = value as Partial<MatchSession>
  return (
    typeof candidate.matchId === 'string' &&
    candidate.matchId.trim().length > 0 &&
    typeof candidate.token === 'string' &&
    candidate.token.trim().length > 0
  )
}

export const matchSessionStorage = {
  read(): MatchSession | null {
    // A token without its match id is unsafe to reuse. Remove the legacy value
    // instead of guessing which room it belongs to.
    localStorage.removeItem(LEGACY_SESSION_TOKEN)

    const raw = localStorage.getItem(STORAGE_MATCH_SESSION)
    if (!raw) return null

    try {
      const parsed: unknown = JSON.parse(raw)
      if (!isMatchSession(parsed)) {
        localStorage.removeItem(STORAGE_MATCH_SESSION)
        return null
      }
      return {
        matchId: parsed.matchId.trim(),
        token: parsed.token.trim(),
      }
    } catch {
      localStorage.removeItem(STORAGE_MATCH_SESSION)
      return null
    }
  },

  write(matchId: string, token: string): MatchSession {
    const session = {
      matchId: matchId.trim(),
      token: token.trim(),
    }
    if (!isMatchSession(session)) {
      throw new Error('A valid match id and session token are required.')
    }
    localStorage.setItem(STORAGE_MATCH_SESSION, JSON.stringify(session))
    localStorage.removeItem(LEGACY_SESSION_TOKEN)
    return session
  },

  get(matchId: string): MatchSession | null {
    const session = this.read()
    if (!session || session.matchId.toLowerCase() !== matchId.trim().toLowerCase()) {
      return null
    }
    return session
  },

  clear(matchId?: string): void {
    if (matchId) {
      const current = this.read()
      if (current && current.matchId.toLowerCase() !== matchId.trim().toLowerCase()) {
        return
      }
    }
    localStorage.removeItem(STORAGE_MATCH_SESSION)
    localStorage.removeItem(LEGACY_SESSION_TOKEN)
  },
}
