// https://nuxt.com/docs/api/configuration/nuxt-config
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
    baseURL: process.env.NUXT_APP_BASE_URL || '/',
    buildAssetsDir: '_nuxt/',
  },

  runtimeConfig: {
    public: {
      // Base path of the C# API. Same origin in production (nginx proxies /api to the API container).
      apiBase: '/api',
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
