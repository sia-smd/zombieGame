import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  registerAuthFailureHandler,
  resetAuthFailureNotification,
} from './auth-failure'

const axiosMocks = vi.hoisted(() => {
  let responseReject: ((error: unknown) => Promise<unknown>) | null = null
  const apiClient = Object.assign(vi.fn().mockResolvedValue({ ok: true }), {
    interceptors: {
      request: { use: vi.fn() },
      response: {
        use: vi.fn((_success, reject) => {
          responseReject = reject
        }),
      },
    },
  })
  const refreshClient = {
    post: vi.fn(),
  }
  const create = vi
    .fn()
    .mockReturnValueOnce(apiClient)
    .mockReturnValueOnce(refreshClient)

  return {
    apiClient,
    refreshClient,
    create,
    responseReject: () => responseReject,
  }
})

vi.mock('axios', () => ({
  default: {
    create: axiosMocks.create,
  },
}))

import { authSession } from './api'

function unauthorized(config: Record<string, unknown>) {
  return {
    config,
    response: { status: 401 },
    message: 'Unauthorized',
  }
}

describe('API refresh and auth failure lifecycle', () => {
  beforeEach(() => {
    localStorage.clear()
    resetAuthFailureNotification()
    axiosMocks.apiClient.mockClear()
    axiosMocks.refreshClient.post.mockReset()
  })

  it('retries an unauthorized request once after cookie refresh', async () => {
    authSession.markSignedIn()
    axiosMocks.refreshClient.post.mockResolvedValueOnce({ data: {} })
    const reject = axiosMocks.responseReject()
    expect(reject).not.toBeNull()
    const config = { headers: {} as Record<string, string> }

    await reject!(unauthorized(config))

    expect(axiosMocks.refreshClient.post).toHaveBeenCalledTimes(1)
    expect(axiosMocks.refreshClient.post).toHaveBeenCalledWith('/api/account/refresh-token', {})
    expect(axiosMocks.apiClient).toHaveBeenCalledTimes(1)
    expect(config).toMatchObject({ _retry: true })
    expect(config.headers.Authorization).toBeUndefined()
  })

  it('notifies auth teardown once when cookie refresh fails', async () => {
    authSession.markSignedIn()
    axiosMocks.refreshClient.post.mockRejectedValue(new Error('refresh rejected'))
    const teardown = vi.fn().mockResolvedValue(undefined)
    registerAuthFailureHandler(teardown)
    const reject = axiosMocks.responseReject()

    await expect(reject!(unauthorized({ headers: {} }))).rejects.toMatchObject({
      message: 'Unauthorized',
    })
    await expect(reject!(unauthorized({ headers: {} }))).rejects.toMatchObject({
      message: 'Unauthorized',
    })

    expect(teardown).toHaveBeenCalledTimes(1)
    expect(authSession.isSignedIn()).toBe(false)
    expect(localStorage.getItem('zvh_access_token')).toBeNull()
    expect(localStorage.getItem('zvh_refresh_token')).toBeNull()
  })
})
