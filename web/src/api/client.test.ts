import { describe, expect, it } from 'vitest'
import { ApiError, friendlyMessage } from './client'

describe('friendlyMessage', () => {
  it('prefers the server detail for validation problems', () => {
    expect(friendlyMessage(400, { title: 'validation_failed', detail: 'Please fix 1 field(s): email - Enter a valid email address' })).toContain('Enter a valid email')
  })
  it('falls back to field errors when there is no detail', () => {
    expect(friendlyMessage(400, { errors: { email: ['Enter a valid email address.'] } })).toBe('email: Enter a valid email address.')
  })
  it('explains network failures', () => {
    expect(new ApiError(0, { title: 'network_error' }).message).toMatch(/Cannot reach/)
  })
  it('hides stack details on server errors but keeps the correlation reference', () => {
    const message = friendlyMessage(500, { correlationId: 'abc123' })
    expect(message).toContain('reference abc123')
  })
  it('gives rate-limit and permission errors plain-language messages', () => {
    expect(friendlyMessage(429, {})).toMatch(/Too many attempts/)
    expect(friendlyMessage(403, {})).toMatch(/permission/)
  })
})
