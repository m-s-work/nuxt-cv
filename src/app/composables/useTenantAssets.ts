import { errorMessage, type AdminFile } from '~/composables/useAdmin'

/** Asset URL as used in the CV ("/api/assets/<file>"). */
export const ASSET_PREFIX = '/api/assets/'

/** Safe asset file name for an upload (the API only accepts [A-Za-z0-9._-]). */
export function assetFileName(name: string): string {
  return name.replace(/[^A-Za-z0-9._-]/g, '-').replace(/^[^A-Za-z0-9]+/, '')
}

export function isImageFile(path: string): boolean {
  return /\.(png|jpe?g|gif|webp|avif|svg|ico)$/i.test(path)
}

/**
 * Asset files of a tenant for the admin's pickers: the list, uploads and thumbnails (object URLs, since asset
 * requests need the admin key header). Shared by all pickers of the page.
 */
export function useTenantAssets(tenantId: string) {
  const admin = useAdmin()
  const files = useState<AdminFile[] | null>(`tenant-assets-${tenantId}`, () => null)
  const thumbs = useState<Record<string, string>>(`tenant-asset-thumbs-${tenantId}`, () => ({}))
  const loading = ref(false)
  const error = ref('')

  const assets = computed(() => (files.value ?? []).filter(f => f.path.startsWith('assets/')))

  async function load(force = false) {
    if (files.value && !force) return
    loading.value = true
    error.value = ''
    try {
      files.value = await admin.files(tenantId)
    } catch (e) {
      error.value = errorMessage(e)
    } finally {
      loading.value = false
    }
  }

  /** Object URL of an asset ("/api/assets/x.png" or "assets/x.png"), loaded once. */
  function thumb(url: string): string | undefined {
    const name = url.startsWith(ASSET_PREFIX) ? url.slice(ASSET_PREFIX.length) : url.replace(/^assets\//, '')
    if (!(name in thumbs.value)) {
      // Deferred: called while rendering.
      queueMicrotask(() => {
        if (name in thumbs.value) return
        thumbs.value[name] = ''
        admin.readBlob(tenantId, `assets/${name}`)
          .then((blob) => { thumbs.value[name] = URL.createObjectURL(blob) })
          .catch(() => { /* missing asset: no thumbnail */ })
      })
    }
    return thumbs.value[name] || undefined
  }

  /** Uploads files and returns their CV URLs. */
  async function upload(selected: File[]): Promise<string[]> {
    const urls: string[] = []
    error.value = ''
    for (const file of selected) {
      const name = assetFileName(file.name)
      await admin.writeFile(tenantId, `assets/${name}`, file)
      urls.push(ASSET_PREFIX + name)
    }
    await load(true)
    return urls
  }

  return { assets, loading, error, load, thumb, upload }
}
