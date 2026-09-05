import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { getActivePinia } from 'pinia'
import {
  notifyAuthFailure,
  resetAuthFailureNotification,
} from './auth-failure'
import { useUiStore } from '@/stores/ui.store'

const STORAGE_AUTH = 'zvh_auth'
const LEGACY_ACCESS = 'zvh_access_token'
const LEGACY_REFRESH = 'zvh_refresh_token'

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

/** Signed-in flag only — JWT lives in httpOnly cookies, never in JS storage. */
export const authSession = {
  isSignedIn(): boolean {
    return localStorage.getItem(STORAGE_AUTH) === '1'
  },
  markSignedIn() {
    localStorage.removeItem(LEGACY_ACCESS)
    localStorage.removeItem(LEGACY_REFRESH)
    localStorage.setItem(STORAGE_AUTH, '1')
    resetAuthFailureNotification()
  },
  clear() {
    localStorage.removeItem(STORAGE_AUTH)
    localStorage.removeItem(LEGACY_ACCESS)
    localStorage.removeItem(LEGACY_REFRESH)
  },
}

if (typeof localStorage !== 'undefined') {
  localStorage.removeItem(LEGACY_ACCESS)
  localStorage.removeItem(LEGACY_REFRESH)
}

export const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30_000,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

const refreshApi = axios.create({
  baseURL: API_BASE_URL,
  timeout: 30_000,
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  trackWrite(config, 1)
  return config
})

let refreshPromise: Promise<boolean> | null = null

async function refreshAccessToken(): Promise<boolean> {
  if (!authSession.isSignedIn()) return false

  try {
    await refreshApi.post('/api/account/refresh-token', {})
    authSession.markSignedIn()
    return true
  } catch {
    authSession.clear()
    return false
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
    const ok = await refreshPromise
    if (!ok) {
      await notifyAuthFailure()
      return Promise.reject(error)
    }

    return api(original)
  },
)

export function createIdempotencyKey(): string {
  return crypto.randomUUID()
}
