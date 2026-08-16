import { images } from '@/assets/images'
import { avatarUrlFromId } from '@/utils/avatarAssets'

export function playerAvatarUrl(imageId?: string | null): string {
  if (!imageId) return images.avatars.default
  return avatarUrlFromId(imageId)
}
