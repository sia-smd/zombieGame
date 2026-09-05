import { onMounted, onUnmounted, watch } from 'vue'
import { gameAudio } from '@/services/game-audio'
import { useRoomStore } from '@/stores/room.store'
import { useSettingsStore } from '@/stores/settings.store'
import { RoomPhase } from '@/types/enums'
import { resolveBackgroundTrack } from '@/utils/gameAudioTracks'

const UNLOCK_EVENTS: Array<keyof WindowEventMap> = ['pointerdown', 'keydown', 'touchstart']

/** Loops room/event music and plays battle stings plus a click on every real button. */
export function useGameAudio() {
  const room = useRoomStore()
  const settings = useSettingsStore()

  function sync() {
    gameAudio.setMusicEnabled(settings.musicEnabled)
    gameAudio.setSoundEnabled(settings.soundEnabled)
    const track = room.matchId
      ? resolveBackgroundTrack(room.currentPhase, room.currentDayEvent)
      : null
    gameAudio.playMusic(track)
  }

  function unlock() {
    gameAudio.unlock()
    sync()
    for (const event of UNLOCK_EVENTS) {
      window.removeEventListener(event, unlock)
    }
  }

  function onButtonClick(event: MouseEvent) {
    const target = event.target
    if (!(target instanceof Element)) return
    const button = target.closest('button')
    if (!button || button.disabled) return
    gameAudio.playSfx('buttonPress')
  }

  onMounted(() => {
    sync()
    document.addEventListener('click', onButtonClick, true)
    for (const event of UNLOCK_EVENTS) {
      window.addEventListener(event, unlock, { once: true, passive: true })
    }
  })

  watch(
    [
      () => room.matchId,
      () => room.currentPhase,
      () => room.currentDayEvent,
      () => settings.musicEnabled,
      () => settings.soundEnabled,
    ],
    sync,
  )

  watch(
    () => room.currentPhase,
    (phase, previous) => {
      if (phase === RoomPhase.BattlePreparation && previous !== RoomPhase.BattlePreparation) {
        gameAudio.playSfx('startPlayCard')
      }
    },
    { immediate: true },
  )

  onUnmounted(() => {
    document.removeEventListener('click', onButtonClick, true)
    for (const event of UNLOCK_EVENTS) {
      window.removeEventListener(event, unlock)
    }
  })
}
