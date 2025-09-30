import type { H3Event } from 'h3'

export interface AuthConfig {
  enabled: boolean
  type: 'token' | 'basic'
  token?: string
  username?: string
  password?: string
}

// Authentication configuration (can be extended with env variables)
const authConfig: AuthConfig = {
  enabled: process.env.AUTH_ENABLED === 'true',
  type: (process.env.AUTH_TYPE as 'token' | 'basic') || 'token',
  token: process.env.AUTH_TOKEN,
  username: process.env.AUTH_USERNAME,
  password: process.env.AUTH_PASSWORD
}

export function isAuthenticated(event: H3Event): boolean {
  if (!authConfig.enabled) {
    return true
  }

  const authHeader = getHeader(event, 'authorization')

  if (!authHeader) {
    return false
  }

  if (authConfig.type === 'token') {
    const token = authHeader.replace('Bearer ', '')
    return token === authConfig.token
  }

  if (authConfig.type === 'basic') {
    const base64Credentials = authHeader.replace('Basic ', '')
    const credentials = Buffer.from(base64Credentials, 'base64').toString('ascii')
    const [username, password] = credentials.split(':')
    return username === authConfig.username && password === authConfig.password
  }

  return false
}

export function requireAuth(event: H3Event) {
  if (!isAuthenticated(event)) {
    throw createError({
      statusCode: 401,
      message: 'Unauthorized'
    })
  }
}
