import { describe, it, expect, beforeEach } from 'vitest'
import type { H3Event } from 'h3'

// Mock environment
beforeEach(() => {
  process.env.AUTH_ENABLED = 'false'
})

describe('Auth System', () => {
  it('should allow access when auth is disabled', async () => {
    // Import dynamically after env is set
    const { isAuthenticated } = await import('~/server/utils/auth')
    
    const mockEvent = {
      headers: {}
    } as unknown as H3Event
    
    expect(isAuthenticated(mockEvent)).toBe(true)
  })
})
