import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { getActivePinia } from 'pinia'
import {
  notifyAuthFailure,
  resetAuthFailureNotification,
} from './auth-failure'
import { useUiStore } from '@/stores/ui.store'

const STORAGE_ACCESS = 'zvh_access_token'
const STORAGE_REFRESH = 'zvh_refresh_token'

export function requireConfiguredUrl(value: string | undefined, _name?: string): string {
  return (value ?? '').trim().replace(/\/$/, '')
}

const API_BASE_URL = requireConfiguredUrl(import.meta.env.VITE_API_BASE_URL)

type RetryableRequestConfig = InternalAxiosRequestConfig & {
  _retry?: boolean
  skipGlobalBusy?: boolean
}

function isWriteRequest(config: InternalAxiosRequestConfig) {
  const method = (config.method ?? 'get').toLowerCase()
  if (method === 'get' || method === 'head' || method === 'options') return false
  const url = `${config.baseURL ?? ''}${config.url ?? ''}`
  if (url.includes('/api/account/refresh-token')) return false
  return !(config as RetryableRequestConfig).skipGlobalBusy
}

function trackWrite(config: InternalAxiosRequestConfig | undefined, delta: 1 | -1) {
  if (!config || !isWriteRequest(config) || !getActivePinia()) return
  const ui = useUiStore()
  if (delta > 0) ui.beginWrite()
  else ui.endWrite()
}

export const tokenStorage = {
  getAccess: () => localStorage.getItem(STORAGE_ACCESS),
  getRefresh: () => localStorage.getItem(STORAGE_REFRESH),
  set(access: string, refresh?: string | null) {
    localStorage.setItem(STORAGE_ACCESS, access)
    if (refresh) {
      localStorage.setItem(STORAGE_REFRESH, refresh)
    } else {
      localStorage.removeItem(STORAGE_REFRESH)
    }
    resetAuthFailureNotification()
  },
  clear() {
    localStorage.removeItem(STORAGE_ACCESS)
    localStorage.removeItem(STORAGE_REFRESH)
  },
}

export const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
})

// Token refresh must use the same API origin without going through the
// authenticated client's 401 interceptor, which would recursively refresh.
const refreshApi = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStorage.getAccess()
  if (token) config.headers.Authorization = `Bearer ${token}`
  trackWrite(config, 1)
  return config
})

let refreshPromise: Promise<string | null> | null = null

async function refreshAccessToken(): Promise<string | null> {
  const refresh = tokenStorage.getRefresh()
  if (!refresh) return null

  try {
    const { data } = await refreshApi.post<{
      accessToken: string
      refreshToken: string
    }>('/api/account/refresh-token', { refreshToken: refresh })
    tokenStorage.set(data.accessToken, data.refreshToken)
    return data.accessToken
  } catch {
    tokenStorage.clear()
    return null
  }
}

api.interceptors.response.use(
  (res) => {
    trackWrite(res.config, -1)
    return res
  },
  async (error: AxiosError) => {
    trackWrite(error.config, -1)
    const original = error.config as RetryableRequestConfig | undefined
    if (!original || error.response?.status !== 401) return Promise.reject(error)
    if (original._retry) {
      await notifyAuthFailure()
      return Promise.reject(error)
    }
    original._retry = true

    if (!refreshPromise) refreshPromise = refreshAccessToken().finally(() => { refreshPromise = null })
    const token = await refreshPromise
    if (!token) {
      await notifyAuthFailure()
      return Promise.reject(error)
    }

    original.headers.Authorization = `Bearer ${token}`
    return api(original)
  },
)

export function createIdempotencyKey(): string {
  return crypto.randomUUID()
}
