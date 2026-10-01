import { describe, it, expect, vi } from 'vitest'
import { MAX_DWELL_MS, PointerDwell, isRetryableStatus, sendWithRetry } from '~/utils/tracking'

describe('PointerDwell', () => {
  const a = { anchor: 'section:hero', x: 10, y: 20 }
  const b = { anchor: 'experience:1', x: 50, y: 60 }

  it('attributes the time between samples to the position the cursor rested at', () => {
    const dwell = new PointerDwell()
    expect(dwell.move(1000, 0, 0, a)).toBeNull()              // nothing rested anywhere yet
    expect(dwell.move(1500, 100, 0, b)).toEqual(['section:hero', 10, 20, 500])
    expect(dwell.move(1800, 200, 0, null)).toEqual(['experience:1', 50, 60, 300])
    expect(dwell.move(2500, 300, 0, a)).toBeNull()            // rested outside any anchor
  })

  it('caps long rests', () => {
    const dwell = new PointerDwell()
    dwell.move(0, 0, 0, a)
    expect(dwell.move(60_000, 100, 0, b)?.[3]).toBe(MAX_DWELL_MS)
  })

  it('flushes the last position once (page hidden or closed)', () => {
    const dwell = new PointerDwell()
    dwell.move(1000, 0, 0, a)
    dwell.move(1400, 100, 0, b)
    expect(dwell.flush(2100)).toEqual(['experience:1', 50, 60, 700])
    expect(dwell.flush(2500)).toBeNull()
    // After the page is visible again, the next move does not count the hidden time.
    expect(dwell.move(9000, 300, 0, a)).toBeNull()
  })

  it('samples at most every 100 ms and after moving 8 px', () => {
    const dwell = new PointerDwell()
    expect(dwell.accepts(0, 0, 0)).toBe(true)
    dwell.move(0, 0, 0, a)
    expect(dwell.accepts(50, 100, 0)).toBe(false)
    expect(dwell.accepts(150, 3, 3)).toBe(false)
    expect(dwell.accepts(150, 10, 0)).toBe(true)
  })
})

describe('sendWithRetry', () => {
  const wait = () => Promise.resolve()

  it('retries a failed request once with the same call', async () => {
    const send = vi.fn()
      .mockRejectedValueOnce(new TypeError('network'))
      .mockResolvedValueOnce({ ok: true, status: 204 })
    await expect(sendWithRetry(send, { wait })).resolves.toBe(true)
    expect(send).toHaveBeenCalledTimes(2)
  })

  it('gives up after the retry and never rejects', async () => {
    const send = vi.fn().mockRejectedValue(new TypeError('network'))
    await expect(sendWithRetry(send, { wait })).resolves.toBe(false)
    expect(send).toHaveBeenCalledTimes(2)
  })

  it('retries server errors but not client errors', async () => {
    const server = vi.fn().mockResolvedValueOnce({ ok: false, status: 503 }).mockResolvedValueOnce({ ok: true, status: 204 })
    await expect(sendWithRetry(server, { wait })).resolves.toBe(true)
    const client = vi.fn().mockResolvedValue({ ok: false, status: 400 })
    await expect(sendWithRetry(client, { wait })).resolves.toBe(false)
    expect(client).toHaveBeenCalledTimes(1)
    expect(isRetryableStatus(429)).toBe(true)
  })

  it('waits before retrying', async () => {
    const delays: number[] = []
    const send = vi.fn().mockResolvedValueOnce({ ok: false, status: 500 }).mockResolvedValueOnce({ ok: true, status: 204 })
    await sendWithRetry(send, { delayMs: 1234, wait: async (ms) => { delays.push(ms) } })
    expect(delays).toEqual([1234])
  })
})
