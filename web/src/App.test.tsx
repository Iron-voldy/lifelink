import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { App } from './App'
import { AuthProvider } from './features/auth/AuthContext'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })

describe('admin application', () => {
  it('protects admin routes and completes login', async () => {
    sessionStorage.clear()
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/api/auth/login')) return json({ accessToken: 'access', accessTokenExpiresAtUtc: new Date().toISOString(), refreshToken: 'refresh', refreshTokenExpiresAtUtc: new Date().toISOString() })
      if (url.endsWith('/api/auth/me')) return json({ id: 'admin-1', email: 'admin@lifelink.lk', role: 'BloodBankAdmin' })
      if (url.includes('/api/requests/reports/summary')) return json({ total: 0, open: 0, critical: 0, fulfilled: 0, closedUnfulfilled: 0, unitsByBloodType: {} })
      if (url.includes('/api/agent-workflows')) return json([])
      if (url.includes('/api/inventory/reports/stock-levels')) return json([])
      return json({ title: 'Not found' }, 404)
    }))
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={client}><MemoryRouter initialEntries={['/dashboard']}><AuthProvider><App /></AuthProvider></MemoryRouter></QueryClientProvider>)
    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
    const user = userEvent.setup()
    await user.type(screen.getByLabelText('Email address'), 'admin@lifelink.lk')
    await user.type(screen.getByLabelText('Password'), 'StrongPassword!42')
    await user.click(screen.getByRole('button', { name: 'Sign in securely' }))
    expect(await screen.findByRole('heading', { name: 'Command overview' })).toBeInTheDocument()
    expect(screen.getByText('admin@lifelink.lk')).toBeInTheDocument()
  })
})
