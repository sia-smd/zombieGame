import { images } from '@/assets/images'
import { DayEventType, PlayerRole } from '@/types/enums'

const ROLE_CARD_IDS = new Set([
  '11111111-1111-1111-1111-111111111108',
  '11111111-1111-1111-1111-111111111101',
  '11111111-1111-1111-1111-111111111106',
])

export const ActionCardIds = {
  shotgun: '11111111-1111-1111-1111-111111111103',
  heal: '11111111-1111-1111-1111-111111111104',
  shield: '11111111-1111-1111-1111-111111111105',
  visitor: '11111111-1111-1111-1111-111111111109',
  poison: '11111111-1111-1111-1111-111111111110',
  pass: '11111111-1111-1111-1111-111111111111',
} as const

const ACTION_CARD_IDS = new Set<string>([
  ActionCardIds.visitor,
  ActionCardIds.poison,
  ActionCardIds.pass,
])

const CARD_BY_ID: Record<string, string> = {
  [ActionCardIds.shotgun]: images.cards.shotgun,
  [ActionCardIds.heal]: images.cards.heal,
  [ActionCardIds.shield]: images.cards.shield,
  [ActionCardIds.visitor]: images.cards.visitor,
  [ActionCardIds.poison]: images.cards.zombiePoison,
  [ActionCardIds.pass]: images.cards.pass,
}

export function isSelfTargetCard(cardId: string): boolean {
  const id = cardId.toLowerCase()
  return id === ActionCardIds.shield || id === ActionCardIds.visitor || id === ActionCardIds.pass
}

const CARD_BY_SLUG: Record<string, string> = {
  shield: images.cards.shield,
  heal: images.cards.heal,
  shotgun: images.cards.shotgun,
  human: images.cards.visitor,
  visitor: images.cards.visitor,
  'human-role': images.cards.visitor,
  zombie: images.cards.zombiePoison,
  poison: images.cards.zombiePoison,
  pass: images.cards.pass,
}

export function getCardImage(cardId: string, name?: string): string {
  const id = cardId.toLowerCase()
  if (CARD_BY_ID[id]) return CARD_BY_ID[id]
  if (CARD_BY_SLUG[id]) return CARD_BY_SLUG[id]

  const lowerName = (name ?? '').toLowerCase()
  if (lowerName.includes('shield') || lowerName.includes('barricade') || lowerName.includes('سپر')) {
    return images.cards.shield
  }
  if (lowerName.includes('heal') || lowerName.includes('medkit') || lowerName.includes('درمان')) {
    return images.cards.heal
  }
  if (lowerName.includes('shotgun') || lowerName.includes('شات')) {
    return images.cards.shotgun
  }
  if (lowerName.includes('infect') || lowerName.includes('zombie') || lowerName.includes('مسموم') || lowerName.includes('poison')) {
    return images.cards.zombiePoison
  }
  if (lowerName.includes('visitor') || lowerName.includes('human') || lowerName.includes('نقش')) {
    return images.cards.visitor
  }
  if (lowerName.includes('pass') || lowerName.includes('پاس')) {
    return images.cards.pass
  }

  return images.cards.back
}

export function isRoleCardId(cardId: string): boolean {
  return ROLE_CARD_IDS.has(cardId.toLowerCase())
}

export function isActionCardId(cardId: string): boolean {
  return ACTION_CARD_IDS.has(cardId.toLowerCase())
}

export function getRoleImage(role: PlayerRole): string {
  switch (role) {
    case PlayerRole.Human:
      return images.roles.human
    case PlayerRole.Zombie:
      return images.roles.zombie
    case PlayerRole.PowerZombie:
      return images.roles.powerZombie
    default:
      return images.cards.back
  }
}

export function getDayEventImage(event: DayEventType): string {
  switch (event) {
    case DayEventType.SunnyDay:
      return images.events.sunnyDay
    case DayEventType.Storm:
      return images.events.storm
    default:
      return images.events.normalDay
  }
}

/** Public copies of the Unity event art (jpg) — used when the bundled png is missing at runtime. */
export function getDayEventPublicImage(event: DayEventType): string {
  switch (event) {
    case DayEventType.SunnyDay:
      return '/images/event/EventSunnyDay.jpg'
    case DayEventType.Storm:
      return '/images/event/EventStorm.jpg'
    default:
      return '/images/event/EventNormalDay.jpg'
  }
}
