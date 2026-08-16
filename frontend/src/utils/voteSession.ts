const PREFIX = 'zvh_vote_'

export type StoredVote = {
  selected: string
  hasVoted: boolean
}

function key(matchId: string, dayNumber: number) {
  return `${PREFIX}${matchId.trim().toLowerCase()}_${dayNumber}`
}

export function readStoredVote(matchId: string, dayNumber: number): StoredVote | null {
  try {
    const raw = sessionStorage.getItem(key(matchId, dayNumber))
    if (!raw) return null
    const parsed = JSON.parse(raw) as StoredVote
    if (!parsed?.selected) return null
    return { selected: parsed.selected, hasVoted: !!parsed.hasVoted }
  } catch {
    return null
  }
}

export function writeStoredVote(matchId: string, dayNumber: number, vote: StoredVote) {
  try {
    sessionStorage.setItem(key(matchId, dayNumber), JSON.stringify(vote))
  } catch {
    // ignore quota / private-mode failures
  }
}
