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

import { tokenStorage } from './api'

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

  it('retries an unauthorized request only once after refresh', async () => {
    tokenStorage.set('old-access', 'refresh-token')
    axiosMocks.refreshClient.post.mockResolvedValueOnce({
      data: {
        accessToken: 'new-access',
        refreshToken: 'new-refresh',
      },
    })
    const reject = axiosMocks.responseReject()
    expect(reject).not.toBeNull()
    const config = { headers: {} as Record<string, string> }

    await reject!(unauthorized(config))

    expect(axiosMocks.refreshClient.post).toHaveBeenCalledTimes(1)
    expect(axiosMocks.apiClient).toHaveBeenCalledTimes(1)
    expect(config).toMatchObject({
      _retry: true,
      headers: { Authorization: 'Bearer new-access' },
    })
  })

  it('notifies auth teardown once when refresh fails', async () => {
    tokenStorage.set('access', 'invalid-refresh')
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
    expect(tokenStorage.getAccess()).toBeNull()
    expect(tokenStorage.getRefresh()).toBeNull()
  })
})
