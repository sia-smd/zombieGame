export type AppErrorKind = 'auth' | 'network' | 'server' | 'unknown'

export type NormalizedAppError = {
  kind: AppErrorKind
  message: string
  status: number | null
  code: string | null
  cause: unknown
}

type ErrorShape = {
  code?: unknown
  message?: unknown
  response?: {
    status?: unknown
    data?: unknown
  }
}

function firstMessage(value: unknown): string | null {
  if (typeof value === 'string' && value.trim()) return value.trim()
  if (!value || typeof value !== 'object') return null

  const data = value as {
    message?: unknown
    title?: unknown
    detail?: unknown
    error?: unknown
  }
  for (const candidate of [data.message, data.title, data.detail, data.error]) {
    if (typeof candidate === 'string' && candidate.trim()) return candidate.trim()
  }
  return null
}

export function normalizeAppError(
  error: unknown,
  fallback = 'An unexpected error occurred.',
): NormalizedAppError {
  const shape = error && typeof error === 'object' ? (error as ErrorShape) : null
  const status =
    typeof shape?.response?.status === 'number' ? shape.response.status : null
        const data = shape?.response?.data
        const dataObj = data && typeof data === 'object' ? (data as { code?: unknown }) : null
        const dataCode = typeof dataObj?.code === 'string' ? dataObj.code : null
        const code =
          dataCode ?? (typeof shape?.code === 'string' ? shape.code : null)
  const serverMessage = firstMessage(shape?.response?.data)
  const ownMessage =
    error instanceof Error
      ? error.message.trim()
      : typeof shape?.message === 'string'
        ? shape.message.trim()
        : ''
  const message = unwrapHubErrorMessage((serverMessage ?? ownMessage) || fallback)

  let kind: AppErrorKind = 'unknown'
  if (status === 401 || status === 403) {
    kind = 'auth'
  } else if (
    !status &&
    (code === 'ERR_NETWORK' ||
      code === 'ECONNABORTED' ||
      ownMessage === 'Network Error' ||
      ownMessage.toLowerCase().includes('reconnect'))
  ) {
    kind = 'network'
  } else if (status !== null) {
    kind = 'server'
  }

  return { kind, message, status, code, cause: error }
}

export function errorMessage(error: unknown, fallback?: string): string {
  return normalizeAppError(error, fallback).message
}

/** SignalR wraps unhandled hub faults; keep the inner rule text for the player. */
function unwrapHubErrorMessage(message: string): string {
  const stripped = message
    .replace(/^An unexpected error occurred invoking '[^']+' on the server\.\s*/i, '')
    .replace(/^(HubException|ServiceException):\s*/i, '')
    .trim()
  return stripped || message
}
