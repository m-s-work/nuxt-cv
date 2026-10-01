import { defineConfig, devices } from '@playwright/test'
import { ADMIN_KEY, PORTS } from './stack/config.mjs'

/**
 * End-to-end tests against the full stack: SPA + C# API + PDF renderer on the sample data.
 *
 * - Default: stack/start.mjs starts everything locally (dotnet, node; no Docker needed).
 * - E2E_BASE_URL=http://localhost:8080: test an already running stack instead, e.g. docker compose with
 *   e2e/docker-compose.e2e.yml (see e2e/README.md). The deployment must serve the sample data and use E2E_ADMIN_KEY.
 *
 * Two hosts of the same server: "localhost" is the demo tenant's own host (public profile without invite),
 * "127.0.0.1" is the shared host (showcase, invites of tenants without own host).
 */
const external = process.env.E2E_BASE_URL
const tenantUrl = external || `http://localhost:${PORTS.web}`
const sharedUrl = process.env.E2E_SHARED_URL || tenantUrl.replace('//localhost', '//127.0.0.1')

process.env.E2E_TENANT_URL = tenantUrl
process.env.E2E_SHARED_URL = sharedUrl
process.env.E2E_ADMIN_KEY = ADMIN_KEY

export default defineConfig({
  testDir: './tests',
  // One backend with shared state (invites, tenant.json, consent records): run the files one after another.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: tenantUrl,
    locale: 'en-US',
    viewport: { width: 1440, height: 900 },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } } }
  ],
  webServer: external
    ? undefined
    : {
        command: 'node stack/start.mjs',
        url: `http://localhost:${PORTS.web}/api/health`,
        // First run builds the API and generates the frontend.
        timeout: 600_000,
        reuseExistingServer: !process.env.CI,
        stdout: process.env.E2E_STACK_LOGS ? 'pipe' : 'ignore',
        stderr: 'pipe'
      }
})
