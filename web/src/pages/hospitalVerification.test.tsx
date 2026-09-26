import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'
import { HospitalsPage } from './HospitalsPage'

const hospitals = [
  { id: 'h1', name: 'City Hospital', registrationNumber: 'REG-001', verificationStatus: 'Pending', address: 'Colombo', latitude: null, longitude: null, createdAtUtc: '2026-10-01T00:00:00Z' },
  { id: 'h2', name: 'Lake Hospital', registrationNumber: 'REG-002', verificationStatus: 'Pending', address: 'Kandy', latitude: null, longitude: null, createdAtUtc: '2026-10-01T00:00:00Z' },
]

vi.mock('../api/client', () => ({
  api: {
    hospitals: {
      list: vi.fn(async () => ({ items: hospitals, page: 1, pageSize: 100, totalCount: 2 })),
      verify: vi.fn(async () => { throw new Error('Hospital was not found.') }),
    },
  },
}))

describe('Hospital verification', () => {
  it('shows a failed decision only on the hospital it was made for', async () => {
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><HospitalsPage /></QueryClientProvider>)
    const verifyButtons = await screen.findAllByRole('button', { name: 'Verify' })
    await userEvent.click(verifyButtons[1])
    await waitFor(() => expect(screen.getAllByRole('alert')).toHaveLength(1))
    expect(screen.getByRole('alert').closest('article')).toHaveTextContent('Lake Hospital')
  })
})
