import { DayEventType, RoomPhase } from '@/types/enums'
import type { MusicTrackId } from '@/assets/audio'

/** Background loop for the current room screen. Effects stay on a separate player. */
export function resolveBackgroundTrack(
  phase: RoomPhase,
  dayEvent: DayEventType,
): MusicTrackId | null {
  if (phase === RoomPhase.Finished) return null
  if (phase === RoomPhase.Lobby) return 'waitingRoom'

  if (phase === RoomPhase.Voting || phase === RoomPhase.VoteResult) return 'voting'

  switch (dayEvent) {
    case DayEventType.SunnyDay:
      return 'sunnyDay'
    case DayEventType.Storm:
      return 'stormDay'
    default:
      return 'normalDay'
  }
}
