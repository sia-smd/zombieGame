import { musicTracks, sfxTracks, type MusicTrackId, type SfxId } from '@/assets/audio'

const MUSIC_VOLUME = 0.42
const SFX_VOLUME = 0.85

/**
 * Two HTML audio players: looping background music, and one-shot effects.
 * Short effects clone a dedicated node so a button click cannot cut a sting.
 * Browsers block playback until a user gesture, so call {@link DualAudioEngine.unlock} once.
 */
export class DualAudioEngine {
  private readonly music = new Audio()
  private readonly sfx = new Audio()
  private unlocked = false
  private musicEnabled = true
  private soundEnabled = true
  private desiredTrack: MusicTrackId | null = null
  private currentTrack: MusicTrackId | null = null

  constructor() {
    this.music.loop = true
    this.music.preload = 'auto'
    this.music.volume = MUSIC_VOLUME
    this.sfx.preload = 'auto'
    this.sfx.volume = SFX_VOLUME
  }

  unlock() {
    if (this.unlocked) return
    this.unlocked = true
    void this.syncMusic()
  }

  setMusicEnabled(enabled: boolean) {
    this.musicEnabled = enabled
    void this.syncMusic()
  }

  setSoundEnabled(enabled: boolean) {
    this.soundEnabled = enabled
    if (!enabled) {
      this.sfx.pause()
      this.sfx.removeAttribute('src')
    }
  }

  playMusic(track: MusicTrackId | null) {
    this.desiredTrack = track
    void this.syncMusic()
  }

  playSfx(id: SfxId) {
    if (!this.soundEnabled || !this.unlocked) return
    const src = sfxTracks[id]
    if (id === 'buttonPress') {
      this.playOverlaySfx(src)
      return
    }

    if (this.sfx.src !== src) this.sfx.src = src
    this.sfx.currentTime = 0
    void this.sfx.play().catch(() => undefined)
  }

  stop() {
    this.desiredTrack = null
    this.currentTrack = null
    this.music.pause()
    this.music.removeAttribute('src')
    this.sfx.pause()
  }

  private playOverlaySfx(src: string) {
    const node = this.sfx.cloneNode(true) as HTMLAudioElement
    node.src = src
    node.volume = SFX_VOLUME
    void node.play().catch(() => undefined)
  }

  private async syncMusic() {
    if (!this.musicEnabled || !this.desiredTrack) {
      this.music.pause()
      if (!this.desiredTrack) {
        this.currentTrack = null
        this.music.removeAttribute('src')
      }
      return
    }

    if (this.currentTrack !== this.desiredTrack) {
      this.music.src = musicTracks[this.desiredTrack]
      this.currentTrack = this.desiredTrack
    }

    if (!this.unlocked) return

    try {
      await this.music.play()
    } catch {
      // Autoplay still blocked until the next gesture.
    }
  }
}

export const gameAudio = new DualAudioEngine()
