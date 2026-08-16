import { useI18n } from 'vue-i18n'
import { computed } from 'vue'
import { GamePhase, PlayerRole, RoomPhase } from '@/types/enums'
import { useLanguageStore } from '@/stores/language.store'
import { formatDate, formatNumber } from '@/i18n/helpers/format'

function enumKey(enumObj: Record<string, string | number>, value: number): string {
  const name = enumObj[value] as string
  if (!name) return 'unknown'
  return name.charAt(0).toLowerCase() + name.slice(1)
}

export function useGameLabels() {
  const { t } = useI18n()

  function roomPhaseLabel(phase: RoomPhase): string {
    const key = enumKey(RoomPhase, phase)
    return t(`phases.room.${key}`)
  }

  function gamePhaseLabel(phase: GamePhase): string {
    const key = enumKey(GamePhase, phase)
    return t(`phases.game.${key}`)
  }

  function roleLabel(role: PlayerRole): string {
    const key = enumKey(PlayerRole, role)
    return t(`roles.${key}`)
  }

  return { roomPhaseLabel, gamePhaseLabel, roleLabel }
}

export function roleColorClass(role: PlayerRole): string {
  switch (role) {
    case PlayerRole.Human: return 'text-plague text-glow-green'
    case PlayerRole.Zombie:
    case PlayerRole.PowerZombie:
      return 'text-blood text-glow-red'
    default: return 'text-mist'
  }
}

export function playerInitials(name: string): string {
  return name
    .split(' ')
    .map((p) => p[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()
}

export function useLocaleFormat() {
  const languageStore = useLanguageStore()

  const locale = computed(() => languageStore.language)

  function formatLocaleNumber(value: number, options?: Intl.NumberFormatOptions) {
    return formatNumber(value, locale.value, options)
  }

  function formatLocaleDate(value: Date | string | number, options?: Intl.DateTimeFormatOptions) {
    return formatDate(value, locale.value, options)
  }

  return { locale, formatLocaleNumber, formatLocaleDate }
}
