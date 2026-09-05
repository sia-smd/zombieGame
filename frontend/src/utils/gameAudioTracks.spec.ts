import { describe, expect, it } from 'vitest'
import { DayEventType, RoomPhase } from '@/types/enums'
import { resolveBackgroundTrack } from './gameAudioTracks'

describe('resolveBackgroundTrack', () => {
  it('loops the matching day-event track during the day', () => {
    expect(resolveBackgroundTrack(RoomPhase.DayStart, DayEventType.NormalDay)).toBe('normalDay')
    expect(resolveBackgroundTrack(RoomPhase.OpponentSelection, DayEventType.SunnyDay)).toBe('sunnyDay')
    expect(resolveBackgroundTrack(RoomPhase.CardBattle, DayEventType.Storm)).toBe('stormDay')
    expect(resolveBackgroundTrack(RoomPhase.Discussion, DayEventType.NormalDay)).toBe('normalDay')
  })

  it('uses the voting loop during vote phases', () => {
    expect(resolveBackgroundTrack(RoomPhase.Voting, DayEventType.Storm)).toBe('voting')
    expect(resolveBackgroundTrack(RoomPhase.VoteResult, DayEventType.SunnyDay)).toBe('voting')
  })

  it('loops waiting-room music while players join the lobby', () => {
    expect(resolveBackgroundTrack(RoomPhase.Lobby, DayEventType.NormalDay)).toBe('waitingRoom')
  })

  it('stays silent after the match', () => {
    expect(resolveBackgroundTrack(RoomPhase.Finished, DayEventType.NormalDay)).toBeNull()
  })
})
