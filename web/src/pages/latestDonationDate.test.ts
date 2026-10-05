// @vitest-environment jsdom
import { describe, expect, it } from 'vitest'
import { latestDonationDate } from './DonorsPage'

describe('latestDonationDate', () => {
  it('returns a date in YYYY-MM-DD format', () => {
    expect(latestDonationDate(new Date('2026-10-05T12:00:00Z'))).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('never returns a date later than today in UTC, which is what the server accepts', () => {
    const now = new Date('2026-10-05T23:30:00Z')
    expect(latestDonationDate(now) <= now.toISOString().slice(0, 10)).toBe(true)
  })
})