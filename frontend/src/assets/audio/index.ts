import normalDay from './normal-day.mp3'
import sunnyDay from './sunny-day.mp3'
import stormDay from './storm-day.mp3'
import voting from './voting.mp3'
import waitingRoom from './waiting-room.mp3'
import startPlayCard from './start-paly-card.mp3'
import showResultPlayCard from './show-result-play-card.mp3'
import buttonPress from './button-press.mp3'

export const musicTracks = {
  normalDay,
  sunnyDay,
  stormDay,
  voting,
  waitingRoom,
} as const

export const sfxTracks = {
  startPlayCard,
  showResultPlayCard,
  buttonPress,
} as const

export type MusicTrackId = keyof typeof musicTracks
export type SfxId = keyof typeof sfxTracks
