import { api } from './api'

export interface PlayerStatistics {
  wins: number
  losses: number
  matchesPlayed: number
}

export interface CurrentPlayerProfile {
  playerId: string
  accountType: number
  name: string
  username: string
  imageId: string
  customAvatarData?: string | null
  level: number
  coins: number
  phoneNumber?: string | null
  mobileVerified: boolean
  pendingPhoneNumber?: string | null
  hasPassword: boolean
  inventory: unknown[]
  statistics: PlayerStatistics
  createdDate: string
}

export interface AvatarOption {
  id: string
  label: string
}

export interface UpdateProfilePayload {
  name?: string
  imageId?: string
}

export interface AchievementProgress {
  code: string
  title: string
  description: string
  statKey: string
  threshold: number
  tier: number
  currentValue: number
  unlocked: boolean
  unlockedAt?: string | null
}

export interface PlayerProgress {
  stats: Record<string, number>
  achievements: AchievementProgress[]
}

export const profileService = {
  async getProfile(): Promise<CurrentPlayerProfile> {
    const { data } = await api.get<CurrentPlayerProfile>('/api/profile/me')
    return data
  },

  async getAvatars(): Promise<AvatarOption[]> {
    const { data } = await api.get<AvatarOption[]>('/api/profile/avatars')
    return data
  },

  async getAchievements(): Promise<PlayerProgress> {
    const { data } = await api.get<PlayerProgress>('/api/profile/achievements')
    return data
  },

  async updateProfile(payload: UpdateProfilePayload): Promise<CurrentPlayerProfile> {
    const { data } = await api.put<CurrentPlayerProfile>('/api/profile/update', payload)
    return data
  },

  async updateUsername(username: string): Promise<CurrentPlayerProfile> {
    const { data } = await api.put<CurrentPlayerProfile>('/api/profile/username', { username })
    return data
  },

  async uploadAvatar(imageBase64: string): Promise<CurrentPlayerProfile> {
    const { data } = await api.post<CurrentPlayerProfile>('/api/profile/avatar', { imageBase64 })
    return data
  },

  async addMobile(mobileNumber: string): Promise<{ success: boolean; verificationRequired: boolean; message: string }> {
    const { data } = await api.post('/api/account/add-mobile', { mobileNumber })
    return data
  },

  async verifyMobile(mobileNumber: string, code: string): Promise<{ success: boolean; message: string }> {
    const { data } = await api.post('/api/account/verify-mobile', { mobileNumber, code })
    return data
  },

  async changePassword(newPassword: string, currentPassword?: string): Promise<{ success: boolean; message: string }> {
    const { data } = await api.put('/api/account/change-password', { newPassword, currentPassword })
    return data
  },
}
