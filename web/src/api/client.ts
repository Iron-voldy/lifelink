import type { AttendanceReport, AuthenticatedUser,BloodRequest, BloodRequestStatus, BloodType, Camp, CampRosterEntry, CampSlot, DonationRecord, Donor, DonorProfileInput, EligibilityEvaluation, EligibilityHistoryEntry, EligibilityStatus, Hospital, InventoryLot, Location, PagedResult, ProblemDetails, RequestSummary, StockLevel, TokenPair, VerificationStatus, Workflow, WorkflowStatus } from './types'

const baseUrl = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080').replace(/\/$/, '')
const refreshKey = 'lifelink.refresh-token'
let accessToken: string | null = null
let refreshToken: string | null = sessionStorage.getItem(refreshKey)
let refreshPromise: Promise<TokenPair> | null = null
const sessionEndedListeners = new Set<() => void>()

/** Lets the UI react when the server ends the session (refresh rejected), instead of leaving a half-signed-in screen. */
export function onSessionEnded(listener: () => void) { sessionEndedListeners.add(listener); return () => { sessionEndedListeners.delete(listener) } }

/** Turns a server problem (or a missing one) into a sentence a person can act on. */
export function friendlyMessage(status: number, problem: ProblemDetails): string {
  const fieldErrors = problem.errors ? Object.entries(problem.errors).map(([field, messages]) => `${field}: ${messages[0]}`).join('; ') : ''
  if (status === 0) return problem.detail || 'Cannot reach the LifeLink server. Check your connection and try again.'
  if (status === 401) return problem.detail || 'Your session has expired. Please sign in again.'
  if (status === 403) return 'You do not have permission to do this.'
  if (status === 429) return problem.detail || 'Too many attempts. Please wait a minute and try again.'
  if (status >= 500) return `${problem.detail || 'The server had a problem. Please try again shortly.'}${problem.correlationId ? ` (reference ${problem.correlationId})` : ''}`
  return problem.detail || fieldErrors || problem.title || `Request failed (${status})`
}

export class ApiError extends Error {
  constructor(public status: number, public problem: ProblemDetails) { super(friendlyMessage(status, problem)) }
}

function saveTokens(tokens: TokenPair | null) {
  accessToken = tokens?.accessToken ?? null
  refreshToken = tokens?.refreshToken ?? null
  if (refreshToken) sessionStorage.setItem(refreshKey, refreshToken); else sessionStorage.removeItem(refreshKey)
}

async function parse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let problem: ProblemDetails = { title: response.statusText, status: response.status }
    try {
      const body: unknown = await response.json()
      if (body && typeof body === 'object') problem = body as ProblemDetails
    } catch { /* non-JSON upstream failure (proxy/gateway page) keeps the status text */ }
    throw new ApiError(response.status, problem)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

/** fetch that reports network failures as a typed, human-readable ApiError instead of a bare TypeError. */
async function send(url: string, init: RequestInit): Promise<Response> {
  try { return await fetch(url, init) } catch { throw new ApiError(0, { title: 'network_error', status: 0 }) }
}

async function renew(): Promise<TokenPair> {
  if (!refreshToken) throw new ApiError(401, { title: 'Session expired', status: 401 })
  if (!refreshPromise) {
    refreshPromise = send(`${baseUrl}/api/auth/refresh`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken }) }).then(parse<TokenPair>).then(tokens => { saveTokens(tokens); return tokens })
      .catch(error => {
        // Only a rejected token ends the session. A rate limit or network blip must not silently sign the person out.
        if (error instanceof ApiError && (error.status === 401 || error.status === 400)) { saveTokens(null); sessionEndedListeners.forEach(listener => listener()) }
        throw error
      })
      .finally(() => { refreshPromise = null })
  }
  return refreshPromise
}

async function request<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
  const headers = new Headers(init.headers)
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  const response = await send(`${baseUrl}${path}`, { ...init, headers })
  if (response.status === 401 && retry && refreshToken) { await renew(); return request<T>(path, init, false) }
  return parse<T>(response)
}

const query = (values: Record<string, string | number | boolean | undefined>) => {
  const params = new URLSearchParams(); Object.entries(values).forEach(([key, value]) => { if (value !== undefined && value !== '') params.set(key, String(value)) }); return params.toString()
}

export const api = {
  auth: {
    hasRefreshToken: () => Boolean(refreshToken),
    login: async (email: string, password: string) => { const tokens = await parse<TokenPair>(await send(`${baseUrl}/api/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, deviceName: 'React admin' }) })); saveTokens(tokens); return request<AuthenticatedUser>('/api/auth/me') },
    restore: async () => { await renew(); return request<AuthenticatedUser>('/api/auth/me') },
    register: async (email: string, password: string, role: 'Donor' | 'HospitalRequester') => { const tokens = await parse<TokenPair>(await send(`${baseUrl}/api/auth/register`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, role, deviceName: 'LifeLink web' }) })); saveTokens(tokens); return request<AuthenticatedUser>('/api/auth/me') },
    /** Clears the session locally at once; revoking the refresh token is best-effort and never blocks sign-out. */
    logout: () => { const token = refreshToken; saveTokens(null); if (token) void fetch(`${baseUrl}/api/auth/revoke`, { method: 'POST', keepalive: true, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: token }) }).catch(() => undefined) },
  },
  donors: { list: (filters: { search?: string; eligibilityStatus?: EligibilityStatus; bloodType?: BloodType; includeInactive?: boolean; page?: number; sortBy?: string; descending?: boolean }) => request<PagedResult<Donor>>(`/api/donors?${query({ ...filters, pageSize: 25 })}`), evaluate: (id: string) => request<EligibilityEvaluation>(`/api/donors/${id}/check-eligibility`, { method: 'POST' }), mine: async () => { try { return await request<Donor>('/api/donors/me') } catch (error) { if (error instanceof ApiError && error.status === 404) return null; throw error } }, saveMine: (id: string | undefined, input: DonorProfileInput) => id ? request<Donor>(`/api/donors/${id}`, { method: 'PUT', body: JSON.stringify(input) }) : request<Donor>('/api/donors', { method: 'POST', body: JSON.stringify(input) }), eligibilityHistory: (id: string) => request<EligibilityHistoryEntry[]>(`/api/donors/${id}/eligibility-history`), donationHistory: (id: string) => request<DonationRecord[]>(`/api/donors/${id}/donation-history`), deactivate: (id: string) => request<void>(`/api/donors/${id}`, { method: 'DELETE' }), recordDonation: (id: string, input: { donationDate: string; units: number; location: string; notes?: string }) => request<DonationRecord>(`/api/donors/${id}/donations`, { method: 'POST', body: JSON.stringify(input) }) },
  hospitals: { list: (status?: VerificationStatus) => request<PagedResult<Hospital>>(`/api/hospitals?${query({ status, pageSize: 100 })}`), verify: (id: string, status: VerificationStatus) => request<Hospital>(`/api/hospitals/${id}/verification`, { method: 'PUT', body: JSON.stringify({ status }) }) },
  requests: { list: (status?: BloodRequestStatus) => request<PagedResult<BloodRequest>>(`/api/requests?${query({ status, pageSize: 50 })}`), summary: () => request<RequestSummary>('/api/requests/reports/summary'), transition: (id: string, status: BloodRequestStatus, reason?: string) => request<BloodRequest>(`/api/requests/${id}/status`, { method: 'PUT', body: JSON.stringify({ status, reason }) }) },
  inventory: { list: () => request<PagedResult<InventoryLot>>('/api/inventory?pageSize=100'), locations: () => request<Location[]>('/api/inventory/locations'), createLocation: (input: { name: string; address: string; latitude: number; longitude: number }) => request<Location>('/api/inventory/locations', { method: 'POST', body: JSON.stringify(input) }), stockIn: (input: { locationId: string; bloodType: BloodType; units: number; expiryDate: string; source: string }) => request<InventoryLot>('/api/inventory/stock-in', { method: 'POST', body: JSON.stringify(input) }), adjust: (id: string, unitsAvailable: number, expectedVersion: number) => request<InventoryLot>(`/api/inventory/${id}/adjust`, { method: 'PUT', body: JSON.stringify({ unitsAvailable, expectedVersion }) }), quarantine: (id: string) => request<InventoryLot>(`/api/inventory/${id}`, { method: 'DELETE' }), report: () => request<StockLevel[]>('/api/inventory/reports/stock-levels'), expiring: () => request<InventoryLot[]>('/api/inventory/expiring-soon?days=7') },
  camps: { list: () => request<PagedResult<Camp>>('/api/camps?pageSize=100'), create: (input: { name: string; location: string; startsAtUtc: string; endsAtUtc: string; capacity: number; slotMinutes: number }) => request<Camp>('/api/camps', { method: 'POST', body: JSON.stringify(input) }), transition: (id: string, status: string) => request<Camp>(`/api/camps/${id}/status`, { method: 'PUT', body: JSON.stringify({ status }) }), upcoming: () => request<PagedResult<Camp>>(`/api/camps?${query({ status: 'Scheduled', fromUtc: new Date().toISOString(), pageSize: 100 })}`), slots: (id: string) => request<CampSlot[]>(`/api/camps/${id}/slots`), book: (id: string) => request<CampSlot>(`/api/camps/${id}/slots/book`, { method: 'POST', body: JSON.stringify({ preferredSlotId: null }) }), cancelBooking: (campId: string, slotId: string) => request<void>(`/api/camps/${campId}/slots/${slotId}/booking`, { method: 'DELETE' }), checkIn: (campId: string, slotId: string) => request<CampSlot>(`/api/camps/${campId}/slots/${slotId}/check-in`, { method: 'PUT' }), attendance: (id: string) => request<AttendanceReport>(`/api/camps/${id}/attendance-report`), roster: (id: string) => request<CampRosterEntry[]>(`/api/camps/${id}/roster`) },
  workflows: { list: (status?: WorkflowStatus) => request<Workflow[]>(`/api/agent-workflows?${query({ status, limit: 100 })}`), get: (id: string) => request<Workflow>(`/api/agent-workflows/${id}/execution-summary`), decide: (id: string, action: 'approve' | 'reject' | 'revise', comments: string) => request<Workflow>(`/api/agent-workflows/${id}/${action}`, { method: 'POST', body: JSON.stringify({ comments }) }) },
}