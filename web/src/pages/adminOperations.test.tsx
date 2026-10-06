import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => {
  const donor = { id: 'donor-1', userId: 'u1', email: 'kamal@example.com', bloodType: 'OPositive', dateOfBirth: '1990-01-01', eligibilityStatus: 'Eligible', address: 'Colombo', medicalFlags: [], isActive: true, updatedAtUtc: '2026-10-01T00:00:00Z' }
  const camp = { id: 'camp-1', name: 'qa camp', location: 'Hall', startsAtUtc: '2026-10-04T03:00:00Z', endsAtUtc: '2026-10-04T06:00:00Z', capacity: 3, status: 'InProgress', availableSlots: 2, bookedSlots: 1 }
  const workflow = { id: 'wf-1', bloodRequestId: 'r1', attemptNumber: 1, objective: 'Fulfil request', planJson: '{}', status: 'PendingApproval', correlationId: 'c1', steps: [], approvals: [] }
  const inventoryList = vi.fn(async () => ({ items: [{ id: 'l1', locationId: 'loc', locationName: 'Central', bloodType: 'APositive', unitsReceived: 5, unitsAvailable: 5, expiryDate: '2026-11-01', source: 'x', status: 'Available', version: 1, updatedAtUtc: '2026-10-01T00:00:00Z' }], page: 1, pageSize: 100, totalCount: 205, totalPages: 3 }))
  return {
    donor, camp, workflow,
    inventoryList,
    updateLocation: vi.fn(async (id: string, input: { name: string; address: string; latitude: number; longitude: number }) => ({ id, ...input })),
    recordDonation: vi.fn(async (_id: string, input: { donationDate: string; units: number }) => ({ id: 'd1', donationDate: input.donationDate, units: input.units, location: 'Centre' })),
    checkIn: vi.fn(async () => ({})),
    decide: vi.fn(async (): Promise<unknown> => ({ ...workflow, id: 'wf-2', attemptNumber: 2, objective: 'Revised attempt' })),
    workflowList: vi.fn(async (): Promise<unknown[]> => [workflow]),
  }
})
vi.mock('../api/client', () => ({
  api: {
    donors: { list: vi.fn(async () => ({ items: [mocks.donor], page: 1, pageSize: 25, totalCount: 1, totalPages: 1 })), evaluate: vi.fn(), deactivate: vi.fn(), recordDonation: mocks.recordDonation },
    camps: { list: vi.fn(async () => ({ items: [mocks.camp], page: 1, pageSize: 100, totalCount: 1, totalPages: 1 })), transition: vi.fn(), create: vi.fn(), roster: vi.fn(async () => [{ slotId: 's1', slotTimeUtc: '2026-10-04T03:00:00Z', status: 'Booked', donorId: 'donor-1', donorEmail: 'kamal@example.com', bloodType: 'OPositive' }]), attendance: vi.fn(async () => ({ campId: 'camp-1', capacity: 3, booked: 1, checkedIn: 0, noShows: 0, cancelled: 0, attendanceRate: 0 })), checkIn: mocks.checkIn },
    workflows: { list: mocks.workflowList, get: vi.fn(async (id: string) => id === 'wf-2' ? { ...mocks.workflow, id: 'wf-2', attemptNumber: 2, objective: 'Revised attempt' } : mocks.workflow), decide: mocks.decide },
    inventory: { list: mocks.inventoryList, locations: vi.fn(async () => [{ id: 'loc', name: 'Central', address: 'Colombo', latitude: 6.9, longitude: 79.8 }]), updateLocation: mocks.updateLocation, expiring: vi.fn(async () => []), report: vi.fn(async () => [{ locationId: 'loc', locationName: 'Central', bloodType: 'APositive', availableUnits: 12, lotCount: 2, expiringWithinSevenDays: 0 }]) },
  },
}))

import { DonorsPage, latestDonationDate } from './DonorsPage'
import { CampsPage, validateCampSchedule } from './CampsPage'
import { WorkflowsPage } from './WorkflowsPage'
import { InventoryPage } from './InventoryPage'

const wrap = (node: ReactNode) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{node}</QueryClientProvider>)
afterEach(() => { cleanup(); vi.clearAllMocks() })

describe('donor registry', () => {
  it('records a donation with a date the server accepts', async () => {
    wrap(<DonorsPage />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Record donation' }))
    const form = screen.getByRole('form', { name: 'Record donation' })
    expect(within(form).getByLabelText('Donation date')).toHaveAttribute('max', latestDonationDate())
    await user.type(within(form).getByLabelText('Location'), 'Narahenpita')
    await user.click(within(form).getByRole('button', { name: 'Record donation' }))
    expect(mocks.recordDonation).toHaveBeenCalledWith('donor-1', { donationDate: latestDonationDate(), units: 1, location: 'Narahenpita', notes: undefined })
    expect(await screen.findByRole('status')).toHaveTextContent('90-day cooldown')
  })
  it('never offers a local date that is ahead of the UTC date', () => {
    expect(latestDonationDate(new Date('2026-10-04T20:00:00Z')) <= '2026-10-04').toBe(true)
  })
})

describe('camp attendance', () => {
  it('lists booked donors and checks one in while the camp is in progress', async () => {
    wrap(<CampsPage />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Attendance' }))
    expect(await screen.findByText('kamal@example.com')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Check in' }))
    expect(mocks.checkIn).toHaveBeenCalledWith('camp-1', 's1')
  })
  it('rejects a start date years ahead', () => {
    expect(validateCampSchedule('2030-10-10T09:00', '2030-10-10T12:00', 4, 15, new Date('2026-10-04T08:00:00'))).toMatch(/two years/)
  })
})

describe('workflow decisions', () => {
  it('follows the new attempt after a revision', async () => {
    mocks.workflowList.mockResolvedValueOnce([mocks.workflow]).mockResolvedValue([{ ...mocks.workflow, id: 'wf-2', objective: 'Revised attempt' }, { ...mocks.workflow, status: 'Revising' }])
    wrap(<WorkflowsPage />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Revise' }))
    await user.type(screen.getByLabelText('Decision comments'), 'Use nearer stock')
    await user.click(screen.getByRole('button', { name: 'Revise workflow' }))
    expect(await screen.findByRole('heading', { name: 'Revised attempt' })).toBeInTheDocument()
  })
  it('refreshes the workflow list even when the decision fails', async () => {
    mocks.decide.mockRejectedValueOnce(new Error('Only 1 of the 2 proposed unit(s) are still in stock.'))
    wrap(<WorkflowsPage />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Approve workflow' }))
    expect(await screen.findByText(/still in stock/)).toBeInTheDocument()
    expect(mocks.workflowList.mock.calls.length).toBeGreaterThan(1)
  })
})

describe('inventory totals', () => {
  it('uses the server stock report rather than the listed page of lots', async () => {
    wrap(<InventoryPage />)
    const summary = await screen.findByLabelText('Available units by blood type')
    expect(await within(summary).findByText('12')).toBeInTheDocument()
  })

  it('sends location, blood type, status and sort controls to the inventory query', async () => {
    wrap(<InventoryPage />)
    const user = userEvent.setup()
    await screen.findByLabelText('Available units by blood type')

    await user.selectOptions(screen.getByLabelText('Filter by location'), 'loc')
    await user.selectOptions(screen.getByLabelText('Filter by blood type'), 'ONegative')
    await user.selectOptions(screen.getByLabelText('Filter by status'), 'Expired')
    await user.selectOptions(screen.getByLabelText('Sort by'), 'units')
    await user.selectOptions(screen.getByLabelText('Sort order'), 'true')

    await waitFor(() => expect(mocks.inventoryList).toHaveBeenLastCalledWith(expect.objectContaining({
      locationId: 'loc',
      bloodType: 'ONegative',
      status: 'Expired',
      includeExpired: true,
      sortBy: 'units',
      descending: true,
      page: 1,
      pageSize: 100,
    })))
  })

  it('edits a location and submits its updated details', async () => {
    wrap(<InventoryPage />)
    const user = userEvent.setup()
    await user.click(await screen.findByRole('button', { name: 'Edit' }))

    const form = screen.getByRole('form', { name: 'Edit location' })
    await user.clear(within(form).getByLabelText('Name'))
    await user.type(within(form).getByLabelText('Name'), 'North Central')
    await user.clear(within(form).getByLabelText('Address'))
    await user.type(within(form).getByLabelText('Address'), 'New address')
    await user.clear(within(form).getByLabelText('Latitude'))
    await user.type(within(form).getByLabelText('Latitude'), '7.1')
    await user.clear(within(form).getByLabelText('Longitude'))
    await user.type(within(form).getByLabelText('Longitude'), '80.2')
    await user.click(within(form).getByRole('button', { name: 'Save location' }))

    await waitFor(() => expect(mocks.updateLocation).toHaveBeenCalledWith('loc', {
      name: 'North Central',
      address: 'New address',
      latitude: 7.1,
      longitude: 80.2,
    }))
    expect(await screen.findByRole('status')).toHaveTextContent('Location North Central updated.')
  })

  it('paginates inventory and resets to the first page when page size changes', async () => {
    wrap(<InventoryPage />)
    const user = userEvent.setup()
    await screen.findByLabelText('Available units by blood type')

    await user.click(screen.getByRole('button', { name: 'Next inventory page' }))
    await waitFor(() => expect(mocks.inventoryList).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2, pageSize: 100 })))

    await user.selectOptions(screen.getByLabelText('Lots per page'), '20')
    await waitFor(() => expect(mocks.inventoryList).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, pageSize: 20 })))
    expect(screen.getByText(/Page 1 of 3/)).toBeInTheDocument()
  })
})
