import { images } from '@/assets/images'
const catalogAvatarMap: Record<string, string> = {
  avatar_default_01: images.avatars.default,
  avatar_zombie_01: images.roles.zombie,
  avatar_survivor_01: images.roles.human,
  avatar_medic_01: images.cards.heal,
  avatar_hunter_01: images.cards.shotgun,
}

export function resolveAvatarUrl(
  profile: { imageId: string; customAvatarData?: string | null } | null | undefined,
): string | null {
  if (!profile) return null
  if (profile.imageId === 'avatar_custom' && profile.customAvatarData) {
    return `data:image/jpeg;base64,${profile.customAvatarData}`
  }
  return catalogAvatarMap[profile.imageId] ?? images.avatars.default
}

export function avatarUrlFromId(imageId: string, customAvatarData?: string | null): string {
  if (imageId === 'avatar_custom' && customAvatarData) {
    return `data:image/jpeg;base64,${customAvatarData}`
  }
  return catalogAvatarMap[imageId] ?? images.avatars.default
}
