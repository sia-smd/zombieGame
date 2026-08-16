export type AuthFailureHandler = () => void | Promise<void>

let handler: AuthFailureHandler | null = null
let handlingPromise: Promise<void> | null = null
let notified = false

export function registerAuthFailureHandler(next: AuthFailureHandler): () => void {
  handler = next
  notified = false
  return () => {
    if (handler === next) handler = null
  }
}

export async function notifyAuthFailure(): Promise<void> {
  if (handlingPromise) return handlingPromise
  if (!handler || notified) return

  notified = true
  const run = Promise.resolve().then(() => handler?.())
  handlingPromise = run
  try {
    await run
  } finally {
    if (handlingPromise === run) handlingPromise = null
  }
}

export function resetAuthFailureNotification(): void {
  notified = false
}
