import { api } from './api'

export interface ClientBootStatus {
  serverVersion: string
  minClientVersion: string
  engineHealthy: boolean
  databaseHealthy: boolean
  redisHealthy: boolean | null
}

export const clientService = {
  async boot(): Promise<ClientBootStatus> {
    const { data } = await api.get<ClientBootStatus>('/api/client/boot', { timeout: 8000 })
    return data
  },
}
