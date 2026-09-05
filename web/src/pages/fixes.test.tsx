import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import { AuthProvider } from '../features/auth/AuthContext'
import { formatDate } from '../components/QueryState'
import { validateCampSchedule } from './CampsPage'
import { passwordProblems } from './RegisterPage'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const tokens = { accessToken: 'access', accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z', refreshToken: 'refresh', refreshTokenExpiresAtUtc: '2030-01-01T00:00:00Z' }
const renderApp = (path: string) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={[path]}><AuthProvider><App /></AuthProvider></MemoryRouter></QueryClientProvider>)
afterEach(() => { cleanup(); vi.unstubAllGlobals(); sessionStorage.clear() })

describe('camp schedule validation', () => {
  const now = new Date('2026-10-04T08:00:00')
  it('rejects an end time before the start time', () => expect(validateCampSchedule('2026-10-10T12:00', '2026-10-10T09:00', 4, 15, now)).toBe('End time must be after the start time.'))
  it('rejects an end time equal to the start time', () => expect(validateCampSchedule('2026-10-10T12:00', '2026-10-10T12:00', 4, 15, now)).toBe('End time must be after the start time.'))
  it('rejects a start in the past', () => expect(validateCampSchedule('2026-10-01T09:00', '2026-10-01T12:00', 4, 15, now)).toBe('Start time must be in the future.'))
  it('rejects slots that do not fit', () => expect(validateCampSchedule('2026-10-10T09:00', '2026-10-10T10:00', 10, 15, now)).toMatch(/need 150 minutes/))
  it('accepts a valid schedule', () => expect(validateCampSchedule('2026-10-10T09:00', '2026-10-10T12:00', 10, 15, now)).toBeNull())
})

describe('helpers', () => {
  it('lists every missing password rule', () => expect(passwordProblems('abc')).toEqual(['at least 12 characters', 'an uppercase letter', 'a digit', 'a symbol']))
  it('accepts a strong password', () => expect(passwordProblems('Strong!Pass123')).toEqual([]))
  it('shows calendar dates without an invented time', () => expect(formatDate('2026-09-09')).not.toMatch(/AM|PM|:/))
})

describe('donor journey on the web', () => {
  it('registers a donor, lands in the donor space and can sign out', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/api/auth/register')) { expect(JSON.parse(String(init?.body))).toMatchObject({ role: 'Donor' }); return json(tokens, 201) }
      if (url.endsWith('/api/auth/me')) return json({ id: 'u1', email: 'new.donor@example.com', role: 'Donor' })
      if (url.endsWith('/api/donors/me')) return json({ title: 'donor_not_found' }, 404)
      if (url.endsWith('/api/auth/revoke')) return new Response(null, { status: 204 })
      return json({ title: 'Not found' }, 404)
    })
    vi.stubGlobal('fetch', fetchMock)
    renderApp('/register')
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Email address'), 'new.donor@example.com')
    await user.type(screen.getByLabelText('Password'), 'Strong!Pass123')
    await user.type(screen.getByLabelText('Confirm password'), 'Strong!Pass123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))
    expect(await screen.findByRole('heading', { name: 'Your donor profile' })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Create your donor profile' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
    expect(sessionStorage.getItem('lifelink.refresh-token')).toBeNull()
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/api/auth/revoke'))).toBe(true)
  })

  it('stops a weak password before calling the server', async () => {
    const fetchMock = vi.fn(); vi.stubGlobal('fetch', fetchMock)
    renderApp('/register')
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Email address'), 'x@example.com')
    await user.type(screen.getByLabelText('Password'), 'short')
    await user.type(screen.getByLabelText('Confirm password'), 'short')
    await user.click(screen.getByRole('button', { name: 'Create account' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Password needs at least 12 characters')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sends a hospital user to a page with a working sign out instead of a dead end', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/auth/login')) return json(tokens)
      if (url.endsWith('/api/auth/me')) return json({ id: 'h1', email: 'staff@hospital.lk', role: 'HospitalRequester' })
      return new Response(null, { status: 204 })
    }))
    renderApp('/dashboard')
    const user = userEvent.setup()
    await user.type(await screen.findByLabelText('Email address'), 'staff@hospital.lk')
    await user.type(screen.getByLabelText('Password'), 'Strong!Pass123')
    await user.click(screen.getByRole('button', { name: 'Sign in securely' }))
    expect(await screen.findByRole('heading', { name: 'Continue in the LifeLink app' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Sign out' }))
    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
  })
})
