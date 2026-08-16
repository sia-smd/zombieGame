import { api } from './api'
import type { LoginResponse, RecoverAccountSendResponse, RegisterGuestResponse } from '@/types/api'
import { profileService, type CurrentPlayerProfile } from '@/services/profile.service'
import { DevicePlatform } from '@/types/enums'

export interface LoginRequest {
  phoneNumber: string
  password: string
}

function deviceId(): string {
  const key = 'zvh_device_id'
  let id = localStorage.getItem(key)
  if (!id) {
    id = crypto.randomUUID()
    localStorage.setItem(key, id)
  }
  return id
}

export const authService = {
  async registerGuest(nickname?: string): Promise<RegisterGuestResponse> {
    const { data } = await api.post<RegisterGuestResponse>('/api/account/register-guest', {
      deviceId: deviceId(),
      platform: DevicePlatform.Android,
      appVersion: '1.0.0',
      nickname: nickname?.trim() || null,
    })
    return data
  },

  async login(payload: LoginRequest): Promise<LoginResponse> {
    const { data } = await api.post<LoginResponse>('/api/account/login', {
      ...payload,
      deviceId: deviceId(),
      platform: DevicePlatform.Android,
      appVersion: '1.0.0',
    })
    return data
  },

  async logout(): Promise<void> {
    await api.post('/api/account/logout')
  },

  async sendRecoverAccountOtp(username: string, password: string): Promise<RecoverAccountSendResponse> {
    const { data } = await api.post<RecoverAccountSendResponse>('/api/account/recover/send-otp', {
      username,
      password,
    })
    return data
  },

  async verifyRecoverAccountOtp(
    username: string,
    password: string,
    code: string,
  ): Promise<LoginResponse> {
    const { data } = await api.post<LoginResponse>('/api/account/recover/verify-otp', {
      username,
      password,
      code,
      deviceId: deviceId(),
      platform: DevicePlatform.Android,
      appVersion: '1.0.0',
    })
    return data
  },

  async getProfile(): Promise<CurrentPlayerProfile> {
    return profileService.getProfile()
  },
}
