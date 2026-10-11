import { describe, expect, it } from 'vitest'
import { validateCampSchedule } from './CampsPage'

const now = new Date('2026-10-01T00:00:00Z')
describe('camp hours', () => {
  it('rejects a 12:57 AM start (time picker left on AM)', () => {
    expect(validateCampSchedule('2026-10-12T00:57:00+05:30', '2026-10-12T17:57:00+05:30', 15, 15, now)).toMatch(/6:00 AM and 9:00 PM/)
  })
  it('rejects a camp running past 9:00 PM or into the next day', () => {
    expect(validateCampSchedule('2026-10-12T18:00:00+05:30', '2026-10-12T22:00:00+05:30', 4, 15, now)).toMatch(/Check AM\/PM/)
    expect(validateCampSchedule('2026-10-12T20:00:00+05:30', '2026-10-13T07:00:00+05:30', 4, 15, now)).toMatch(/one day/)
  })
  it('accepts a 1:00 PM – 5:00 PM camp', () => {
    expect(validateCampSchedule('2026-10-12T13:00:00+05:30', '2026-10-12T17:00:00+05:30', 16, 15, now)).toBeNull()
  })
})
