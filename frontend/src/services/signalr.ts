import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { tokenStorage, requireConfiguredUrl } from './api'
import type { SignalREventMap } from '@/types/api'

export type HubName = 'room' | 'game'
export type SignalREvent = keyof SignalREventMap
export type HubConnectionStatus = 'reconnecting' | 'reconnected' | 'closed'

type Handler<T extends SignalREvent> = (payload: SignalREventMap[T]) => void
type ConnectionStatusListener = (status: HubConnectionStatus, hub: HubName) => void

const connections = new Map<HubName, HubConnection>()
const connectionPromises = new Map<HubName, Promise<HubConnection>>()
const handlers = new Map<HubName, Map<SignalREvent, Set<Handler<SignalREvent>>>>()
const wiredEvents = new Map<HubName, Set<SignalREvent>>()
const connectionStatusListeners = new Set<ConnectionStatusListener>()
const intentionalStops = new Set<HubName>()

function getEventHandlers(hub: HubName) {
  if (!handlers.has(hub)) handlers.set(hub, new Map())
  return handlers.get(hub)!
}

function hubUrl(name: HubName): string {
  const base = requireConfiguredUrl(import.meta.env.VITE_HUB_BASE_URL)
  return `${base}/hubs/${name}`
}

function dispatch<T extends SignalREvent>(hub: HubName, event: T, payload: SignalREventMap[T]) {
  const set = getEventHandlers(hub).get(event)
  set?.forEach((handler) => handler(payload))
}

function notifyConnectionStatus(status: HubConnectionStatus, hub: HubName) {
  connectionStatusListeners.forEach((listener) => listener(status, hub))
}

export function onHubConnectionStatus(listener: ConnectionStatusListener): () => void {
  connectionStatusListeners.add(listener)
  return () => connectionStatusListeners.delete(listener)
}

function wireEvent(hub: HubName, connection: HubConnection, event: SignalREvent) {
  if (!wiredEvents.has(hub)) wiredEvents.set(hub, new Set())
  if (wiredEvents.get(hub)!.has(event)) return
  wiredEvents.get(hub)!.add(event)
  connection.on(event, (payload: unknown) => {
    dispatch(hub, event, payload as SignalREventMap[typeof event])
  })
}

function attachConnectionLifecycle(hub: HubName, connection: HubConnection) {
  connection.onreconnecting(() => notifyConnectionStatus('reconnecting', hub))
  connection.onreconnected(() => notifyConnectionStatus('reconnected', hub))
  connection.onclose(() => {
    connections.delete(hub)
    wiredEvents.delete(hub)
    if (intentionalStops.has(hub)) {
      intentionalStops.delete(hub)
      return
    }
    notifyConnectionStatus('closed', hub)
  })
}

export async function connectHub(hub: HubName): Promise<HubConnection> {
  const pending = connectionPromises.get(hub)
  if (pending) return pending

  const existing = connections.get(hub)
  if (existing) {
    if (existing.state === HubConnectionState.Connected) return existing
    if (existing.state === HubConnectionState.Reconnecting) {
      throw new Error(`${hub} hub is still reconnecting.`)
    }

    connections.delete(hub)
    wiredEvents.delete(hub)
  }

  const startPromise = (async () => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl(hub), {
        accessTokenFactory: () => tokenStorage.getAccess() ?? '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(import.meta.env.DEV ? LogLevel.Information : LogLevel.Warning)
      .build()

    attachConnectionLifecycle(hub, connection)
    await connection.start()
    connections.set(hub, connection)

    for (const event of getEventHandlers(hub).keys()) {
      wireEvent(hub, connection, event)
    }

    return connection
  })()

  connectionPromises.set(hub, startPromise)
  try {
    return await startPromise
  } finally {
    if (connectionPromises.get(hub) === startPromise) {
      connectionPromises.delete(hub)
    }
  }
}

export async function disconnectHub(hub: HubName): Promise<void> {
  const connection = connections.get(hub)
  if (!connection) return
  intentionalStops.add(hub)
  try {
    await connection.stop()
  } finally {
    connections.delete(hub)
    wiredEvents.delete(hub)
    intentionalStops.delete(hub)
  }
}

export function getHubConnection(hub: HubName): HubConnection | null {
  return connections.get(hub) ?? null
}

export function onHubEvent<T extends SignalREvent>(
  hub: HubName,
  event: T,
  handler: Handler<T>,
): () => void {
  const map = getEventHandlers(hub)
  if (!map.has(event)) map.set(event, new Set())
  const set = map.get(event)!
  set.add(handler as Handler<SignalREvent>)

  const connection = connections.get(hub)
  if (connection) wireEvent(hub, connection, event)

  return () => {
    set.delete(handler as Handler<SignalREvent>)
  }
}

export async function invokeHub<T = void>(
  hub: HubName,
  method: string,
  ...args: unknown[]
): Promise<T> {
  const connection = await connectHub(hub)
  return connection.invoke<T>(method, ...args)
}

export async function disconnectAllHubs(): Promise<void> {
  await Promise.allSettled([...connectionPromises.values()])
  await Promise.all([...connections.keys()].map((h) => disconnectHub(h)))
}
