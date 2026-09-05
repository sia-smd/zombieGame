import {
  buildGameManifest,
  preloadGameplayChunks,
  type ManifestEntry,
} from './gameManifest'

export interface PreloadProgress {
  loaded: number
  total: number
  currentUrl: string
  errors: string[]
}

export type ProgressCallback = (progress: PreloadProgress) => void

const MAX_RETRIES = 2
const CONCURRENCY = 6

function preloadImage(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve()
    img.onerror = () => reject(new Error(src))
    img.src = src
  })
}

async function preloadAudio(src: string): Promise<void> {
  const resp = await fetch(src, { cache: 'force-cache' })
  if (!resp.ok) throw new Error(src)
  await resp.blob()
}

async function preloadGeneric(src: string): Promise<void> {
  const resp = await fetch(src, { cache: 'force-cache' })
  if (!resp.ok) throw new Error(src)
  await resp.text()
}

async function loadOne(entry: ManifestEntry): Promise<void> {
  switch (entry.category) {
    case 'images':
      return preloadImage(entry.url)
    case 'audio':
      return preloadAudio(entry.url)
    default:
      return preloadGeneric(entry.url)
  }
}

async function loadWithRetry(entry: ManifestEntry, retries: number): Promise<void> {
  for (let attempt = 0; attempt <= retries; attempt++) {
    try {
      await loadOne(entry)
      return
    } catch {
      if (attempt === retries) throw new Error(entry.url)
    }
  }
}

/**
 * Preload every critical game asset with real progress tracking.
 * Non-critical assets are fetched in background after critical ones.
 */
export async function preloadGameAssets(onProgress?: ProgressCallback): Promise<PreloadProgress> {
  const manifest = buildGameManifest()
  const deduped = dedup(manifest)
  const critical = deduped.filter((e) => e.critical)
  const nonCritical = deduped.filter((e) => !e.critical)
  const total = critical.length
  const errors: string[] = []
  let loaded = 0

  const report = (url: string) => {
    onProgress?.({ loaded, total, currentUrl: url, errors })
  }

  // Load critical assets with concurrency limiter
  await runPool(critical, CONCURRENCY, async (entry) => {
    try {
      await loadWithRetry(entry, MAX_RETRIES)
    } catch {
      errors.push(entry.url)
    }
    loaded++
    report(entry.url)
  })

  // Fire-and-forget non-critical + chunk warm-up
  for (const entry of nonCritical) {
    loadWithRetry(entry, 1).catch(() => undefined)
  }
  for (const p of preloadGameplayChunks()) {
    p.catch(() => undefined)
  }

  return { loaded, total, currentUrl: '', errors }
}

function dedup(entries: ManifestEntry[]): ManifestEntry[] {
  const seen = new Set<string>()
  return entries.filter((e) => {
    if (seen.has(e.url)) return false
    seen.add(e.url)
    return true
  })
}

async function runPool<T>(
  items: T[],
  concurrency: number,
  fn: (item: T) => Promise<void>,
): Promise<void> {
  let idx = 0
  const run = async () => {
    while (idx < items.length) {
      const i = idx++
      await fn(items[i])
    }
  }
  await Promise.all(Array.from({ length: Math.min(concurrency, items.length) }, () => run()))
}

/** Legacy API kept for compatibility */
export function collectAssetUrls(value: unknown, bucket: string[] = []): string[] {
  if (typeof value === 'string' && value.length > 0) {
    bucket.push(value)
    return bucket
  }
  if (value && typeof value === 'object') {
    for (const child of Object.values(value as Record<string, unknown>)) {
      collectAssetUrls(child, bucket)
    }
  }
  return bucket
}
