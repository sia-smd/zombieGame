import { images } from '@/assets/images'
import { musicTracks, sfxTracks } from '@/assets/audio'

export type AssetCategory = 'images' | 'audio' | 'chunks'

export interface ManifestEntry {
  url: string
  category: AssetCategory
  critical: boolean
}

function img(url: string, critical = true): ManifestEntry {
  return { url, category: 'images', critical }
}

function audio(url: string, critical = true): ManifestEntry {
  return { url, category: 'audio', critical }
}

/**
 * Flat manifest of every game asset that must be warm before gameplay.
 * Vite-imported URLs already carry content hashes so they are cache-safe.
 */
export function buildGameManifest(): ManifestEntry[] {
  const entries: ManifestEntry[] = []

  // ── Images: backgrounds ──
  entries.push(img(images.backgrounds.login))
  entries.push(img(images.backgrounds.home))
  entries.push(img(images.backgrounds.voting))

  // ── Images: cards ──
  entries.push(img(images.cards.back))
  entries.push(img(images.cards.shield))
  entries.push(img(images.cards.heal))
  entries.push(img(images.cards.shotgun))
  entries.push(img(images.cards.visitor))
  entries.push(img(images.cards.zombiePoison))
  entries.push(img(images.cards.pass))

  // ── Images: roles ──
  entries.push(img(images.roles.human))
  entries.push(img(images.roles.zombie))
  entries.push(img(images.roles.powerZombie))

  // ── Images: day events ──
  entries.push(img(images.events.normalDay))
  entries.push(img(images.events.sunnyDay))
  entries.push(img(images.events.storm))

  // ── Images: UI ──
  entries.push(img(images.brand.logoSmall))
  entries.push(img(images.avatars.default))
  entries.push(img(images.ui.coin))
  entries.push(img(images.ui.matchmaking, false))
  entries.push(img(images.ui.quickMatch, false))
  entries.push(img(images.ui.inputFrameBig))
  entries.push(img(images.ui.inputFrameSmall))
  entries.push(img(images.ui.wood03))
  entries.push(img(images.ui.wood02))

  // ── Images: public JPG fallbacks (not in Vite bundle) ──
  entries.push(img('/images/event/EventNormalDay.jpg', false))
  entries.push(img('/images/event/EventSunnyDay.jpg', false))
  entries.push(img('/images/event/EventStorm.jpg', false))

  // ── Audio: music tracks ──
  for (const src of Object.values(musicTracks)) {
    entries.push(audio(src))
  }

  // ── Audio: sound effects ──
  for (const src of Object.values(sfxTracks)) {
    entries.push(audio(src))
  }
  return entries
}

/**
 * Route chunk warm-up imports.
 * These are the same dynamic imports as the router, so Vite resolves them
 * to the same hashed chunks — no duplicate code is generated.
 */
export function preloadGameplayChunks(): Promise<unknown>[] {
  return [
    import('@/pages/Home/HomePage.vue'),
    import('@/pages/Lobby/LobbyPage.vue'),
    import('@/pages/RevealRole/RevealRolePage.vue'),
    import('@/pages/GameRoom/GameRoomPage.vue'),
    import('@/pages/Game/GamePage.vue'),
    import('@/pages/BattleSummary/BattleSummaryPage.vue'),
    import('@/pages/Voting/VotingPage.vue'),
    import('@/pages/MatchResult/MatchResultPage.vue'),
  ]
}
