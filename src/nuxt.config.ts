// https://nuxt.com/docs/api/configuration/nuxt-config
const baseURL = process.env.NUXT_APP_BASE_URL || '/'
const apiBase = process.env.NUXT_PUBLIC_API_BASE || '/api'

export default defineNuxtConfig({
  compatibilityDate: '2025-07-15',
  devtools: { enabled: true },
  
  modules: [
    '@nuxt/ui',
    '@nuxtjs/i18n'
  ],

  // Import Tailwind CSS
  css: [
    '~/assets/css/main.css',
    // Bundled fonts for the print/PDF layout (no network needed in the PDF renderer)
    '@fontsource-variable/inter',
    '@fontsource-variable/source-serif-4',
    '@fontsource/lato/300.css',
    '@fontsource/lato/400.css',
    '@fontsource/lato/700.css'
  ],

  // Static SPA; served by nginx in the web container (see Dockerfile)
  ssr: false,

  app: {
    baseURL,
    buildAssetsDir: '_nuxt/',
    head: {
      // The API draws the tenant's favicon (tenant.json "favicon"); the .ico is the default for browsers
      // without SVG favicons.
      link: [
        { rel: 'icon', type: 'image/svg+xml', href: `${apiBase}/favicon.svg` },
        { rel: 'icon', type: 'image/x-icon', href: `${baseURL}favicon.ico`, sizes: '16x16 32x32 48x48' }
      ]
    }
  },

  runtimeConfig: {
    public: {
      // Base path of the C# API. Same origin in production (nginx proxies /api to the API container).
      apiBase,
      // Invite code of a public demo CV (e.g. "demo"); the showcase links to it when set. Build time.
      demoInviteCode: process.env.NUXT_PUBLIC_DEMO_INVITE_CODE || ''
    }
  },

  // Local development: forward /api to `dotnet run` (api/CvApi). Host header is kept,
  // so the API resolves the tenant from "localhost" like it would from a real hostname.
  nitro: {
    devProxy: {
      '/api': { target: process.env.CV_API_URL || 'http://localhost:5080/api', changeOrigin: false }
    }
  },

  // Icons are bundled into the client (the static SPA has no icon server endpoint, and the
  // admin page should not call a third-party CDN). Scan finds icons used in app/; the list adds
  // icons Nuxt UI uses internally.
  icon: {
    provider: 'none',
    clientBundle: {
      scan: true,
      icons: ['lucide:chevron-down', 'lucide:chevron-up', 'lucide:check', 'lucide:loader-circle', 'lucide:x', 'lucide:minus']
    }
  },

  // i18n configuration
  i18n: {
    locales: [
      { code: 'en', iso: 'en-US', name: 'English' },
      { code: 'de', iso: 'de-DE', name: 'Deutsch' }
    ],
    defaultLocale: 'en',
    strategy: 'prefix_except_default',
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'i18n_redirected',
      redirectOn: 'root'
    }
  }
})
