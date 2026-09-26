import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const { verify } = vi.hoisted(() => ({ verify: vi.fn(async () => ({})) }))
vi.mock('../api/client', () => ({
  api: {
    hospitals: {
      list: vi.fn(async () => ({
        items: [{ id: 'hospital-1', name: 'Colombo General', registrationNumber: 'H-100', verificationStatus: 'Pending', address: 'Colombo' }],
        page: 1,
        pageSize: 100,
        totalCount: 1,
        totalPages: 1,
      })),
      verify,
    },
  },
}))

import { HospitalsPage } from './HospitalsPage'

describe('hospital verification', () => {
  beforeEach(() => verify.mockClear())

  it('allows an administrator to verify a pending hospital', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={client}><HospitalsPage /></QueryClientProvider>)

    expect(await screen.findByText('Colombo General')).toBeInTheDocument()
    await userEvent.setup().click(screen.getByRole('button', { name: 'Verify' }))
    expect(verify).toHaveBeenCalledWith('hospital-1', 'Verified')
  })
})
