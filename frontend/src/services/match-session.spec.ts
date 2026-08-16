import { beforeEach, describe, expect, it } from 'vitest'
import { matchSessionStorage } from './match-session'

describe('matchSessionStorage', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('returns a token only for the match it belongs to', () => {
    matchSessionStorage.write('match-a', 'token-a')

    expect(matchSessionStorage.get('MATCH-A')).toEqual({
      matchId: 'match-a',
      token: 'token-a',
    })
    expect(matchSessionStorage.get('match-b')).toBeNull()
  })

  it('removes legacy and malformed session values', () => {
    localStorage.setItem('zvh_session_token', 'unsafe-token')
    localStorage.setItem('zvh_match_session', '{"matchId":42}')

    expect(matchSessionStorage.read()).toBeNull()
    expect(localStorage.getItem('zvh_session_token')).toBeNull()
    expect(localStorage.getItem('zvh_match_session')).toBeNull()
  })

  it('does not clear a different active match', () => {
    matchSessionStorage.write('match-b', 'token-b')

    matchSessionStorage.clear('match-a')

    expect(matchSessionStorage.get('match-b')?.token).toBe('token-b')
  })
})
