import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import { api } from '../api/client'
import { AuthProvider } from '../features/auth/AuthContext'
import { validateHospital } from './HospitalPortalPage'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const tokens = { accessToken: 'access', accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z', refreshToken: 'refresh', refreshTokenExpiresAtUtc: '2030-01-01T00:00:00Z' }
const renderApp = (path: string) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter initialEntries={[path]}><AuthProvider><App /></AuthProvider></MemoryRouter></QueryClientProvider>)
const hospital = (verificationStatus: string) => ({ id: 'hosp1', name: 'Teaching Hospital Kandy', registrationNumber: 'DEMO-HOSP-002', verificationStatus, address: 'Kandy', latitude: null, longitude: null, createdAtUtc: '2026-10-01T00:00:00Z' })
const request = (status: string, urgency = 'Routine') => ({ id: 'r1', hospitalId: 'hosp1', hospitalName: 'Teaching Hospital Kandy', requestedByUserId: 'h1', bloodType: 'ONegative', quantityUnits: 2, urgency, status, notes: 'Emergency caesarean section.', requiredByUtc: '2030-01-02T00:00:00Z', createdAtUtc: '2026-10-01T00:00:00Z', updatedAtUtc: '2026-10-01T00:00:00Z' })
const page = (items: unknown[]) => ({ items, page: 1, pageSize: 50, totalCount: items.length, totalPages: 1 })
const summary = { total: 1, open: 1, critical: 0, fulfilled: 0, closedUnfulfilled: 0, unitsByBloodType: {} }
// The client keeps the refresh token in memory, so sign out or the next test silently restores this session.
afterEach(() => { cleanup(); api.auth.logout(); vi.unstubAllGlobals(); sessionStorage.clear() })

async function signIn() {
  const user = userEvent.setup()
  await user.type(await screen.findByLabelText('Email address'), 'hospital12@demo.lifelink.local')
  await user.type(screen.getByLabelText('Password'), 'Strong!Pass123')
  await user.click(screen.getByRole('button', { name: 'Sign in securely' }))
  return user
}

describe('hospital registration validation', () => {
  const valid = { name: 'Teaching Hospital Kandy', registrationNumber: 'moh-hosp-42', address: 'Kandy', position: 'Blood bank officer' }
  it('accepts a lowercase registration number because the API upper-cases it', () => expect(validateHospital(valid)).toBeNull())
  it('rejects a name without letters', () => expect(validateHospital({ ...valid, name: '1234' })).toBe('Hospital name must be 2-250 characters and contain letters.'))
  it('rejects a registration number with spaces', () => expect(validateHospital({ ...valid, registrationNumber: 'MOH 42' })).toMatch(/^Registration number must be/))
  it('rejects a short address and position', () => {
    expect(validateHospital({ ...valid, address: 'K' })).toBe('Address must be 3-500 characters.')
    expect(validateHospital({ ...valid, position: 'x' })).toBe('Your position must be 2-150 characters.')
  })
})

describe('hospital portal', () => {
  it('lets an unlinked hospital user register and then explains verification is pending', async () => {
    let registered: unknown
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/api/auth/login')) return json(tokens)
      if (url.endsWith('/api/auth/me')) return json({ id: 'h1', email: 'hospital12@demo.lifelink.local', role: 'HospitalRequester' })
      if (url.endsWith('/api/hospitals/me')) return json({ title: 'hospital_profile_required', status: 404 }, 404)
      if (url.endsWith('/api/hospitals/register')) { registered = JSON.parse(String(init?.body)); return json(hospital('Pending'), 201) }
      if (url.includes('/api/requests')) return json(page([]))
      return json([])
    }))
    renderApp('/login')
    const user = await signIn()
    expect(await screen.findByRole('heading', { name: 'Register hospital' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Hospital name'), 'Teaching Hospital Kandy')
    await user.type(screen.getByLabelText('Registration number'), 'DEMO-HOSP-002')
    await user.type(screen.getByLabelText('Your position'), 'Blood bank officer')
    await user.type(screen.getByLabelText('Address'), 'Kandy')
    await user.click(screen.getByRole('button', { name: 'Submit for verification' }))
    expect(await screen.findByText(/A blood-bank administrator is reviewing your registration/)).toBeInTheDocument()
    expect(registered).toMatchObject({ name: 'Teaching Hospital Kandy', registrationNumber: 'DEMO-HOSP-002', position: 'Blood bank officer', latitude: null, longitude: null })
    expect(screen.queryByRole('heading', { name: 'New blood request' })).not.toBeInTheDocument()
  })

  it('lets a verified hospital submit and escalate a blood request', async () => {
    const calls: { url: string; method: string; body?: unknown }[] = []
    let current = request('Submitted')
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input); const method = init?.method ?? 'GET'
      calls.push({ url, method, body: init?.body ? JSON.parse(String(init.body)) : undefined })
      if (url.endsWith('/api/auth/login')) return json(tokens)
      if (url.endsWith('/api/auth/me')) return json({ id: 'h1', email: 'hospital12@demo.lifelink.local', role: 'HospitalRequester' })
      if (url.endsWith('/api/hospitals/me')) return json(hospital('Verified'))
      if (url.endsWith('/api/requests/reports/summary')) return json(summary)
      if (url.endsWith('/api/requests/r1/escalate')) { current = request('Escalated', 'Urgent'); return json(current) }
      if (url.endsWith('/api/requests') && method === 'POST') return json(request('Submitted', 'Urgent'), 201)
      if (url.includes('/api/requests?')) return json(page([current]))
      return json([])
    }))
    renderApp('/login')
    const user = await signIn()
    expect(await screen.findByRole('heading', { name: 'New blood request' })).toBeInTheDocument()
    await user.clear(screen.getByLabelText('Units')); await user.type(screen.getByLabelText('Units'), '3')
    await user.click(screen.getByRole('button', { name: 'Submit request' }))
    expect(await screen.findByText('Request submitted with urgent urgency.')).toBeInTheDocument()
    const created = calls.find(x => x.url.endsWith('/api/requests') && x.method === 'POST')
    expect(created?.body).toMatchObject({ bloodType: 'OPositive', quantityUnits: 3, requestedUrgency: 'Routine', notes: null })

    const card = (await screen.findByText('Emergency caesarean section.')).closest('article')!
    await user.click(within(card).getByRole('button', { name: 'Escalate' }))
    expect(await screen.findByText('Escalated to urgent urgency.')).toBeInTheDocument()
    expect(calls.some(x => x.url.endsWith('/api/requests/r1/escalate') && x.method === 'POST')).toBe(true)
  })

  it('hides request actions while the hospital is suspended', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/auth/login')) return json(tokens)
      if (url.endsWith('/api/auth/me')) return json({ id: 'h1', email: 'hospital10@demo.lifelink.local', role: 'HospitalRequester' })
      if (url.endsWith('/api/hospitals/me')) return json(hospital('Suspended'))
      if (url.includes('/api/requests?')) return json(page([request('Submitted')]))
      return json([])
    }))
    renderApp('/login')
    await signIn()
    expect(await screen.findByText(/Your hospital is suspended/)).toBeInTheDocument()
    expect(await screen.findByText('Emergency caesarean section.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Escalate' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'New blood request' })).not.toBeInTheDocument()
  })
})
