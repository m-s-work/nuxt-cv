// Ports and admin key of the local e2e stack (stack/start.mjs); shared with playwright.config.ts.
export const PORTS = {
  web: Number(process.env.E2E_WEB_PORT || 4173),
  api: Number(process.env.E2E_API_PORT || 5181),
  pdf: Number(process.env.E2E_PDF_PORT || 3181)
}

export const ADMIN_KEY = process.env.E2E_ADMIN_KEY || 'e2e-admin-key'
