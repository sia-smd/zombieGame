import { images } from '@/assets/images'

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

export function preloadImage(src: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => resolve()
    img.onerror = () => reject(new Error(src))
    img.src = src
  })
}

export async function preloadGameAssets(): Promise<{ loaded: number; total: number }> {
  const urls = [...new Set(collectAssetUrls(images))]
  await Promise.all(urls.map((url) => preloadImage(url)))
  return { loaded: urls.length, total: urls.length }
}
