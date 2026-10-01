import { test as base, expect, request, type APIRequestContext, type Page } from '@playwright/test'

export const TENANT_URL = process.env.E2E_TENANT_URL!
export const SHARED_URL = process.env.E2E_SHARED_URL!
export const ADMIN_KEY = process.env.E2E_ADMIN_KEY!

export interface CreatedInvite {
  id: string
  code: string
  link: string
  pdf?: Array<{ locale: string, ok: boolean }>
}

export interface InviteBody {
  profile: string
  label?: string
  code?: string
  maxUses?: number
  viewOnce?: boolean
  overrides?: Record<string, unknown>
}

/** Admin API client (X-Admin-Key), like the admin page and tools/cv-sync.sh use it. */
export class AdminApi {
  constructor(readonly http: APIRequestContext) {}

  async createInvite(tenant: string, body: InviteBody): Promise<CreatedInvite> {
    const response = await this.http.post(`api/admin/tenants/${tenant}/invites`, { data: body })
    expect(response.status(), await response.text()).toBe(200)
    const json = await response.json()
    return { id: json.invite.id, code: json.code, link: json.link, pdf: json.pdf }
  }

  async readFile(tenant: string, file: string): Promise<string> {
    const response = await this.http.get(`api/admin/tenants/${tenant}/files/${file}`)
    expect(response.ok()).toBeTruthy()
    return response.text()
  }

  async writeFile(tenant: string, file: string, content: string): Promise<void> {
    const response = await this.http.put(`api/admin/tenants/${tenant}/files/${file}`, {
      data: content,
      headers: { 'Content-Type': 'application/octet-stream' }
    })
    expect(response.ok(), await response.text()).toBeTruthy()
  }
}

/** Unique suffix so reruns against a long-running stack never collide. */
export function unique(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 6)}`
}

/** Waits until the CV is rendered and the splash screen is gone. */
export async function waitForCv(page: Page, name: string | RegExp) {
  await expect(page.locator('.splash-wrapper')).toHaveCount(0, { timeout: 15_000 })
  await expect(page.locator('h1.hero-title')).toContainText(name)
}

export const test = base.extend<{ admin: AdminApi }>({
  admin: async ({}, use) => {
    const http = await request.newContext({ baseURL: `${TENANT_URL}/`, extraHTTPHeaders: { 'X-Admin-Key': ADMIN_KEY } })
    await use(new AdminApi(http))
    await http.dispose()
  }
})

export { expect }
