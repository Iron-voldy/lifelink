import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import { AuthProvider } from '../features/auth/AuthContext'
import { validateDonorProfile } from './DonorPortalPage'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const renderApp = (path: string) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={[path]}><AuthProvider><App /></AuthProvider></MemoryRouter></QueryClientProvider>)
afterEach(() => { cleanup(); vi.unstubAllGlobals(); sessionStorage.clear() })

describe('donor profile validation', () => {
  const now = new Date('2026-10-04T08:00:00')
  it('rejects a missing date of birth', () => expect(validateDonorProfile('', 'Colombo', [], now)).toBe('Enter your date of birth.'))
  it('rejects a future date of birth', () => expect(validateDonorProfile('2026-10-05', 'Colombo', [], now)).toBe('Date of birth cannot be in the future.'))
  it('rejects donors under 18', () => expect(validateDonorProfile('2008-10-05', 'Colombo', [], now)).toBe('Donors must be at least 18 years old.'))
  it('accepts a donor turning 18 today', () => expect(validateDonorProfile('2008-10-04', 'Colombo', [], now)).toBeNull())
  it('rejects an implausibly old date of birth', () => expect(validateDonorProfile('1850-01-01', 'Colombo', [], now)).toBe('Date of birth cannot be more than 120 years ago.'))
  it('rejects a blank address', () => expect(validateDonorProfile('1990-01-01', '   ', [], now)).toBe('Address is required.'))
  it('rejects too many or too long medical notes', () => {
    expect(validateDonorProfile('1990-01-01', 'Colombo', Array.from({ length: 21 }, (_, i) => `note ${i}`), now)).toBe('Add at most 20 medical notes.')
    expect(validateDonorProfile('1990-01-01', 'Colombo', ['x'.repeat(101)], now)).toBe('Each medical note must be at most 100 characters.')
  })
})

describe('donor eligibility self-check', () => {
  it('explains that staff must verify instead of promising a booking', async () => {
    const tokens = { accessToken: 'access', accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z', refreshToken: 'refresh', refreshTokenExpiresAtUtc: '2030-01-01T00:00:00Z' }
    const donor = { id: 'd1', userId: 'u1', email: 'donor@example.com', bloodType: 'OPositive', dateOfBirth: '1990-01-01', lastDonationDate: null, eligibilityStatus: 'PendingVerification', address: 'Colombo', latitude: null, longitude: null, medicalFlags: [], isActive: true, createdAtUtc: '2026-10-01T00:00:00Z', updatedAtUtc: '2026-10-01T00:00:00Z' }
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/auth/login')) return json(tokens)
      if (url.endsWith('/api/auth/me')) return json({ id: 'u1', email: 'donor@example.com', role: 'Donor' })
      if (url.endsWith('/api/donors/me')) return json(donor)
      if (url.endsWith('/check-eligibility')) return json({ donorId: 'd1', previousStatus: 'PendingVerification', status: 'PendingVerification', reasons: ['All automatic checks passed; blood bank staff must verify the donor before they can book.'], evaluatedAtUtc: '2026-10-04T00:00:00Z' })
      if (url.includes('/api/camps')) return json({ items: [], page: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
      return json([])
    }))
    renderApp('/donor')
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Email address'), 'donor@example.com')
    await user.type(screen.getByLabelText('Password'), 'Strong!Pass123')
    await user.click(screen.getByRole('button', { name: 'Sign in securely' }))
    await user.click(await screen.findByRole('button', { name: 'Check my eligibility' }))
    expect(await screen.findByText(/staff must verify the donor/)).toBeInTheDocument()
    expect(screen.queryByText(/You can book a camp below/)).toBeNull()
    expect(screen.getByText('Blood bank staff will verify your profile before you can book.')).toBeInTheDocument()
  })
})

describe('donor registration', () => {
  it('explains an invalid email before calling the API', async () => {
    const fetchMock = vi.fn(async (_input: RequestInfo | URL) => json({ title: 'Not found' }, 404)); vi.stubGlobal('fetch', fetchMock)
    renderApp('/register')
    await userEvent.type(screen.getByLabelText('Email address'), 'not-an-email')
    await userEvent.type(screen.getByLabelText('Password'), 'Strong!Pass123')
    await userEvent.type(screen.getByLabelText('Confirm password'), 'Strong!Pass123')
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Enter a valid email address')
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/api/auth/register'))).toBe(false)
  })
})
