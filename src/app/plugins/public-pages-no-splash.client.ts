// The pricing and legal pages show no CV, so no splash screen: decide before the first render (a hideSplash() in
// the page's setup would let the splash flash up briefly). Same state as composables/useSplashScreen.ts.
const PUBLIC_PAGE = /\/(pricing|legal\/(imprint|privacy|terms))\/?$/

export default defineNuxtPlugin(() => {
  if (PUBLIC_PAGE.test(window.location.pathname)) {
    useState('showSplash', () => false).value = false
  }
})
