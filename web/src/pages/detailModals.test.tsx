import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

vi.mock('../api/client', () => ({
  api: {
    hospitals: {
      list: vi.fn(async () => ({ items: [{ id: 'h1', name: 'Colombo General', registrationNumber: 'H-100', verificationStatus: 'Pending', address: 'Regent Street, Colombo', createdAtUtc: '2026-09-01T04:00:00Z' }], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 })),
      verify: vi.fn(),
    },
    donors: {
      list: vi.fn(async () => ({ items: [{ id: 'd1', userId: 'u1', email: 'donor20@demo.lifelink.local', bloodType: 'OPositive', dateOfBirth: '1994-03-14', eligibilityStatus: 'Eligible', address: 'Kamburugamuwa, Matara', latitude: null, longitude: null, medicalFlags: [], isActive: true, updatedAtUtc: '2026-10-11T06:00:00Z' }], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 })),
      eligibilityHistory: vi.fn(async () => [{ id: 'e1', previousStatus: 'PermanentlyIneligible', newStatus: 'Eligible', reason: 'All deterministic eligibility checks passed.', changedAtUtc: '2026-10-11T06:00:00Z' }]),
      donationHistory: vi.fn(async () => [{ id: 'r1', donationDate: '2026-05-01', units: 1, location: 'Regional Blood Centre Matara' }]),
      evaluate: vi.fn(), deactivate: vi.fn(), recordDonation: vi.fn(),
    },
    camps: {
      list: vi.fn(async () => ({ items: [{ id: 'c1', name: 'Badulla Railway Station Camp', location: 'Community Hall, Badulla', latitude: null, longitude: null, startsAtUtc: '2026-10-13T07:30:00Z', endsAtUtc: '2026-10-13T11:30:00Z', capacity: 2, status: 'Draft', availableSlots: 2, bookedSlots: 0 }], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 })),
      slots: vi.fn(async () => [{ id: 's1', campId: 'c1', slotTimeUtc: '2026-10-13T07:30:00Z', status: 'Available', version: 1 }, { id: 's2', campId: 'c1', slotTimeUtc: '2026-10-13T07:45:00Z', status: 'Booked', version: 1 }]),
      transition: vi.fn(), create: vi.fn(),
    },
  },
}))

import { HospitalsPage } from './HospitalsPage'
import { DonorsPage } from './DonorsPage'
import { CampsPage } from './CampsPage'

const renderPage = (page: React.ReactNode) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{page}</QueryClientProvider>)

describe('admin detail modals', () => {
  it('opens a donor in a centred dialog with history, and closes with Escape', async () => {
    const user = userEvent.setup(); renderPage(<DonorsPage />)
    await user.click(await screen.findByRole('button', { name: 'donor20@demo.lifelink.local' }))
    const dialog = screen.getByRole('dialog', { name: 'donor20@demo.lifelink.local' })
    expect(within(dialog).getByText(/age \d+/)).toBeInTheDocument()
    expect(await within(dialog).findByText('Regional Blood Centre Matara')).toBeInTheDocument()
    expect(within(dialog).getByText('Permanently Ineligible → Eligible')).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Close details' })).toHaveFocus()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('opens a camp by clicking its card and lists every slot time', async () => {
    const user = userEvent.setup(); renderPage(<CampsPage />)
    await user.click(await screen.findByText('Community Hall, Badulla', { exact: false }))
    const dialog = screen.getByRole('dialog', { name: 'Badulla Railway Station Camp' })
    expect(await within(dialog).findByRole('list', { name: 'Camp time slots' })).toBeInTheDocument()
    expect(within(dialog).getAllByRole('listitem')).toHaveLength(2)
    await user.click(within(dialog).getByRole('button', { name: 'Close details' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('opens a hospital with its verification actions', async () => {
    const user = userEvent.setup(); renderPage(<HospitalsPage />)
    await user.click(await screen.findByRole('button', { name: 'Colombo General' }))
    const dialog = screen.getByRole('dialog', { name: 'Colombo General' })
    expect(within(dialog).getByText('H-100')).toBeInTheDocument()
    expect(within(dialog).getByText(/Awaiting verification/)).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'Verify' })).toBeEnabled()
  })
})
