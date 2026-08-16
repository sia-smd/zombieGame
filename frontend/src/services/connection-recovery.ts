import { connectHub, type HubName } from './signalr'

export type RecoverableRoom = {
  matchId: string | null
  sessionToken: string | null
  recoverConnection: () => Promise<void>
}

export type ConnectionRecoveryOptions = {
  room: RecoverableRoom
  closeConnectionAlert: () => void
  showConnectionAlert: (retry: () => Promise<void>) => void
  connect?: (hub: HubName) => Promise<unknown>
}

export function createConnectionRecovery(options: ConnectionRecoveryOptions) {
  const connect = options.connect ?? connectHub
  let roomRecovery: Promise<void> | null = null

  async function retry(hub: HubName): Promise<void> {
    const targetHub: HubName = options.room.matchId ? 'room' : hub
    await connect(targetHub)
    if (
      targetHub === 'room' &&
      options.room.matchId &&
      options.room.sessionToken
    ) {
      await options.room.recoverConnection()
    }
  }

  async function afterReconnect(hub: HubName): Promise<void> {
    if (hub !== 'room') {
      if (!options.room.matchId) options.closeConnectionAlert()
      return
    }
    if (roomRecovery) return roomRecovery

    const run = (async () => {
      if (options.room.matchId && options.room.sessionToken) {
        await options.room.recoverConnection()
      }
      options.closeConnectionAlert()
    })()

    roomRecovery = run
    try {
      await run
    } catch {
      options.showConnectionAlert(() => retry(hub))
    } finally {
      if (roomRecovery === run) roomRecovery = null
    }
  }

  return { retry, afterReconnect }
}
