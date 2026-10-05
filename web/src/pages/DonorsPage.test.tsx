// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DonorsPage } from './DonorsPage'

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } })
const donor = { id: 'd1', userId: 'u1', email: 'donor@example.com', bloodType: 'OPositive', dateOfBirth: '1990-01-01', lastDonationDate: null, eligibilityStatus: 'Eligible', address: 'Colombo', latitude: null, longitude: null, medicalFlags: [], isActive: true, createdAtUtc: '2026-10-01T00:00:00Z', updatedAtUtc: '2026-10-01T00:00:00Z' }
const page = { items: [donor], page: 1, pageSize: 25, totalCount: 1, totalPages: 1 }

const renderPage = () => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><DonorsPage /></QueryClientProvider>)
afterEach(() => { cleanup(); vi.unstubAllGlobals(); sessionStorage.clear() })

describe('DonorsPage sorting', () => {
  it('renders the sort dropdown and the donor row', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => json(page)))
    renderPage()
    expect(await screen.findByText('donor@example.com')).toBeTruthy()
    expect(screen.getByLabelText('Sort donors')).toBeTruthy()
  })

  it('sends sortBy=email to the API when Email is chosen', async () => {
    const fetchMock = vi.fn(async () => json(page))
    vi.stubGlobal('fetch', fetchMock)
    renderPage()
    await screen.findByText('donor@example.com')
    await userEvent.selectOptions(screen.getByLabelText('Sort donors'), 'email')
    await waitFor(() => {
      const urls = fetchMock.mock.calls.map(call => String((call as unknown[])[0]))
      expect(urls.some(url => url.includes('sortBy=email'))).toBe(true)
    })
  })
})