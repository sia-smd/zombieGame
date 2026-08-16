import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { translate } from '@/i18n'
import { authService } from '@/services/auth.service'
import { profileService, type CurrentPlayerProfile } from '@/services/profile.service'
import { tokenStorage } from '@/services/api'
import { disconnectAllHubs } from '@/services/signalr'
import {
  matchSessionStorage,
  type MatchSession,
} from '@/services/match-session'
import type { GuestProfile } from '@/types/api'
import { images } from '@/assets/images'
import { resolveAvatarUrl } from '@/utils/avatarAssets'

export const useAuthStore = defineStore('auth', () => {
  const userId = ref<string | null>(localStorage.getItem('zvh_player_id'))
  const username = ref<string | null>(null)
  const guestProfile = ref<GuestProfile | null>(null)
  const profile = ref<CurrentPlayerProfile | null>(null)
  const matchSession = ref<MatchSession | null>(matchSessionStorage.read())
  const accessToken = ref<string | null>(tokenStorage.getAccess())
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  let profileLoad: Promise<void> | null = null

  const isAuthenticated = computed(() => !!accessToken.value)
  const isGuest = computed(() => (profile.value?.accountType ?? 0) === 0)
  const resolvedUserId = computed(() => userId.value ?? localStorage.getItem('zvh_player_id'))
  const displayName = computed(
    () => profile.value?.name ?? guestProfile.value?.name ?? username.value ?? null,
  )
  const coinCount = computed(() => profile.value?.coins ?? guestProfile.value?.coins ?? 0)
  const avatarUrl = computed(
    () =>
      resolveAvatarUrl(profile.value) ??
      resolveAvatarUrl(guestProfile.value) ??
      images.avatars.default,
  )

  function hydrateFromStorage() {
    if (!userId.value) userId.value = localStorage.getItem('zvh_player_id')
    accessToken.value = tokenStorage.getAccess()
    matchSession.value = matchSessionStorage.read()
  }

  function setUserId(id: string) {
    userId.value = id
    localStorage.setItem('zvh_player_id', id)
  }

  function setTokens(access: string, refresh?: string | null) {
    tokenStorage.set(access, refresh)
    accessToken.value = access
  }

  async function initGuest(nickname?: string) {
    hydrateFromStorage()
    if (accessToken.value) {
      await loadProfile()
      return
    }
    isLoading.value = true
    error.value = null
    try {
      const res = await authService.registerGuest(nickname)
      setUserId(res.playerId)
      setTokens(res.accessToken, res.refreshToken)
      guestProfile.value = res.profile
      await loadProfile()
    } catch (e) {
      error.value = translate('toast.guestRegisterFailed')
      throw e
    } finally {
      isLoading.value = false
    }
  }

  async function applyAccountSession(res: {
    playerId: string
    username?: string
    accessToken: string
    refreshToken: string
    profile: GuestProfile
  }) {
    const [{ useRoomStore }, { useGameStore }] = await Promise.all([
      import('@/stores/room.store'),
      import('@/stores/game.store'),
    ])
    await disconnectAllHubs().catch(() => undefined)
    useRoomStore().reset()
    useGameStore().reset()
    clearMatchSession()
    setUserId(res.playerId)
    username.value = res.username ?? null
    setTokens(res.accessToken, res.refreshToken)
    guestProfile.value = res.profile
    await loadProfile()
  }

  async function login(phoneNumber: string, password: string) {
    isLoading.value = true
    error.value = null
    try {
      const res = await authService.login({ phoneNumber, password })
      await applyAccountSession(res)
    } catch (e) {
      error.value = translate('login.invalidCredentials')
      throw e
    } finally {
      isLoading.value = false
    }
  }

  async function sendRecoverAccountOtp(usernameValue: string, password: string) {
    isLoading.value = true
    error.value = null
    try {
      return await authService.sendRecoverAccountOtp(usernameValue, password)
    } catch (e) {
      error.value = translate('settings.recoverFailed')
      throw e
    } finally {
      isLoading.value = false
    }
  }

  async function verifyRecoverAccountOtp(usernameValue: string, password: string, code: string) {
    isLoading.value = true
    error.value = null
    try {
      const res = await authService.verifyRecoverAccountOtp(usernameValue, password, code)
      await applyAccountSession(res)
      return res
    } catch (e) {
      error.value = translate('settings.recoverOtpInvalid')
      throw e
    } finally {
      isLoading.value = false
    }
  }

  function applyLoadedProfile(next: CurrentPlayerProfile) {
    profile.value = next
    username.value = next.username
    if (guestProfile.value) {
      guestProfile.value = {
        ...guestProfile.value,
        name: next.name,
        imageId: next.imageId,
        coins: next.coins,
        wins: next.statistics.wins,
        losses: next.statistics.losses,
      }
    }
  }

  async function loadProfile() {
    if (profileLoad) return profileLoad
    profileLoad = (async () => {
      try {
        applyLoadedProfile(await profileService.getProfile())
      } catch {
        // Keep last known guest/profile snapshot if the endpoint is unavailable.
      } finally {
        profileLoad = null
      }
    })()
    return profileLoad
  }

  function applyProfile(next: CurrentPlayerProfile) {
    applyLoadedProfile(next)
  }

  function setMatchSession(matchId: string, token: string) {
    matchSession.value = matchSessionStorage.write(matchId, token)
  }

  function getMatchSession(matchId: string): MatchSession | null {
    const current = matchSession.value
    if (current?.matchId.toLowerCase() === matchId.trim().toLowerCase()) {
      return current
    }
    const stored = matchSessionStorage.get(matchId)
    matchSession.value = stored
    return stored
  }

  function clearMatchSession(matchId?: string) {
    matchSessionStorage.clear(matchId)
    if (
      !matchId ||
      matchSession.value?.matchId.toLowerCase() === matchId.trim().toLowerCase()
    ) {
      matchSession.value = null
    }
  }

  async function clearClientSession() {
    await disconnectAllHubs().catch(() => undefined)

    const [{ useRoomStore }, { useGameStore }] = await Promise.all([
      import('@/stores/room.store'),
      import('@/stores/game.store'),
    ])
    useRoomStore().reset()
    useGameStore().reset()

    tokenStorage.clear()
    accessToken.value = null
    userId.value = null
    username.value = null
    profile.value = null
    guestProfile.value = null
    clearMatchSession()
    localStorage.removeItem('zvh_player_id')
  }

  async function logout() {
    try {
      await authService.logout()
    } catch {
      // Local logout must still complete when the API is unavailable.
    }
    await clearClientSession()
  }

  return {
    userId,
    username,
    guestProfile,
    profile,
    matchSession,
    accessToken,
    isLoading,
    error,
    isAuthenticated,
    isGuest,
    resolvedUserId,
    displayName,
    coinCount,
    avatarUrl,
    hydrateFromStorage,
    setUserId,
    initGuest,
    login,
    sendRecoverAccountOtp,
    verifyRecoverAccountOtp,
    loadProfile,
    applyProfile,
    setMatchSession,
    getMatchSession,
    clearMatchSession,
    clearClientSession,
    logout,
    setTokens,
  }
})
