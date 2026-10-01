/**
 * Paddle.js (Billing v2) overlay checkout, loaded on demand from Paddle's CDN only when a user buys a pass
 * (docs/REQUIREMENTS_SAAS.md §5 S5.2). No card data touches our servers.
 */

const PADDLE_SRC = 'https://cdn.paddle.com/paddle/v2/paddle.js'

export interface PaddleEvent {
  name?: string
  data?: unknown
}

interface PaddleGlobal {
  Environment: { set: (env: string) => void }
  Initialize: (options: { token: string, eventCallback?: (event: PaddleEvent) => void }) => void
  Checkout: {
    open: (options: { items: Array<{ priceId: string, quantity: number }>, customer?: { email: string }, customData?: Record<string, string> }) => void
  }
}

let loading: Promise<PaddleGlobal> | null = null
let initializedToken: string | null = null
// Paddle.Initialize is called once per page; events go to the handler of the component that opened the checkout.
let handler: ((event: PaddleEvent) => void) | null = null

function loadScript(): Promise<PaddleGlobal> {
  const existing = (window as unknown as { Paddle?: PaddleGlobal }).Paddle
  if (existing) return Promise.resolve(existing)
  loading ??= new Promise<PaddleGlobal>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = PADDLE_SRC
    script.async = true
    script.onload = () => {
      const paddle = (window as unknown as { Paddle?: PaddleGlobal }).Paddle
      if (paddle) resolve(paddle)
      else reject(new Error('Paddle.js did not load'))
    }
    script.onerror = () => {
      loading = null
      reject(new Error('Paddle.js could not be loaded (blocked by the browser or an extension?)'))
    }
    document.head.appendChild(script)
  })
  return loading
}

/** Loads and initialises Paddle.js (once) and routes its events to `onEvent`. */
export async function initPaddle(config: { clientToken: string, environment?: string | null }, onEvent: (event: PaddleEvent) => void) {
  const paddle = await loadScript()
  handler = onEvent
  if (initializedToken !== config.clientToken) {
    if (config.environment === 'sandbox') paddle.Environment.set('sandbox')
    paddle.Initialize({ token: config.clientToken, eventCallback: event => handler?.(event) })
    initializedToken = config.clientToken
  }
  return paddle
}
