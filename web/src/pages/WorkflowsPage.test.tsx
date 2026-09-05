import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
const { workflow, decide } = vi.hoisted(() => {
  const value = { id: 'workflow-1', bloodRequestId: 'request-1', attemptNumber: 1, objective: 'Fulfil critical O-negative request', planJson: '{"steps":["analyse","validate"]}', status: 'PendingApproval' as const, correlationId: 'corr-1', startedAtUtc: '2026-08-18T10:00:00Z', steps: [{ id: 'step-1', sequence: 1, agentName: 'ValidationSafetyAgent', inputJson: '{}', outputJson: '{"requiresApproval":true}', toolCallsJson: '[]', status: 'Completed' }], approvals: [] }
  return { workflow: value, decide: vi.fn(async (_id: string, _action: string, _comments: string) => ({ ...value, status: 'Rejected' as const })) }
})
vi.mock('../api/client', () => ({ api: { workflows: { list: vi.fn(async () => [workflow]), get: vi.fn(async () => workflow), decide } } }))

import { WorkflowsPage } from './WorkflowsPage'

describe('workflow monitor', () => {
  beforeEach(() => decide.mockClear())
  it('shows persisted execution details and submits a rejection', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={client}><WorkflowsPage /></QueryClientProvider>)
    expect(await screen.findByRole('heading', { name: 'Fulfil critical O-negative request' })).toBeInTheDocument()
    expect(screen.getByText('ValidationSafetyAgent')).toBeInTheDocument()
    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: 'Reject' }))
    await user.type(screen.getByLabelText('Decision comments'), 'Insufficient clinical confirmation')
    await user.click(screen.getByRole('button', { name: 'Reject workflow' }))
    expect(decide).toHaveBeenCalledWith('workflow-1', 'reject', 'Insufficient clinical confirmation')
  })
})
