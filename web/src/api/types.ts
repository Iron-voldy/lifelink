export type UserRole = 'Donor' | 'HospitalRequester' | 'BloodBankAdmin' | 'CampCoordinator'
export type BloodType = 'APositive' | 'ANegative' | 'BPositive' | 'BNegative' | 'ABPositive' | 'ABNegative' | 'OPositive' | 'ONegative'
export type EligibilityStatus = 'PendingVerification' | 'Eligible' | 'TemporarilyIneligible' | 'PermanentlyIneligible'
export type RequestUrgency = 'Routine' | 'Urgent' | 'Critical'
export type BloodRequestStatus = 'Submitted' | 'UnderReview' | 'PendingApproval' | 'Approved' | 'Dispatched' | 'Fulfilled' | 'Escalated' | 'ClosedUnfulfilled'
export type InventoryLotStatus = 'Available' | 'Reserved' | 'Dispatched' | 'Expired' | 'Quarantined'
export type CampStatus = 'Draft' | 'Scheduled' | 'InProgress' | 'Closed' | 'Cancelled'
export type WorkflowStatus = 'Pending' | 'Running' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Revising' | 'Completed' | 'EscalationRequired' | 'Failed'
export type VerificationStatus = 'Pending' | 'Verified' | 'Rejected' | 'Suspended'

export interface TokenPair { accessToken: string; accessTokenExpiresAtUtc: string; refreshToken: string; refreshTokenExpiresAtUtc: string }
export interface AuthenticatedUser { id: string; email: string; role: UserRole }
export interface PagedResult<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export interface Donor { id: string; userId: string; email: string; bloodType: BloodType; dateOfBirth: string; lastDonationDate?: string; eligibilityStatus: EligibilityStatus; address: string; latitude?: number | null; longitude?: number | null; medicalFlags: string[]; isActive: boolean; updatedAtUtc: string }
export interface BloodRequest { id: string; hospitalId: string; hospitalName: string; bloodType: BloodType; quantityUnits: number; urgency: RequestUrgency; status: BloodRequestStatus; notes?: string; requiredByUtc: string; createdAtUtc: string }
export interface Hospital { id: string; name: string; registrationNumber: string; verificationStatus: VerificationStatus; address: string; latitude?: number; longitude?: number; createdAtUtc: string }
export interface RequestSummary { total: number; open: number; critical: number; fulfilled: number; closedUnfulfilled: number; unitsByBloodType: Record<string, number> }
export interface Location { id: string; name: string; address: string; latitude: number; longitude: number }
export interface InventoryLot { id: string; locationId: string; locationName: string; bloodType: BloodType; unitsReceived: number; unitsAvailable: number; expiryDate: string; source: string; status: InventoryLotStatus; version: number; updatedAtUtc: string }
export interface StockLevel { locationId: string; locationName: string; bloodType: BloodType; availableUnits: number; lotCount: number; expiringWithinSevenDays: number }
export interface Camp { id: string; name: string; location: string; startsAtUtc: string; endsAtUtc: string; capacity: number; status: CampStatus; availableSlots: number; bookedSlots: number }
export interface WorkflowStep { id: string; sequence: number; agentName: string; inputJson: string; outputJson?: string; toolCallsJson: string; status: string; errorCode?: string; errorMessage?: string }
export interface Approval { id: string; version: number; approverUserId: string; decision: string; comments?: string; decidedAtUtc: string }
export interface Workflow { id: string; bloodRequestId: string; attemptNumber: number; objective: string; planJson: string; status: WorkflowStatus; correlationId: string; startedAtUtc?: string; completedAtUtc?: string; finalOutcomeJson?: string; steps: WorkflowStep[]; approvals: Approval[] }
export interface DonorProfileInput { bloodType: BloodType; dateOfBirth: string; address: string; latitude: number | null; longitude: number | null; medicalFlags: string[] }
export interface EligibilityEvaluation { donorId: string; previousStatus: EligibilityStatus; status: EligibilityStatus; reasons: string[]; evaluatedAtUtc: string }
export interface EligibilityHistoryEntry { id: string; previousStatus: EligibilityStatus; newStatus: EligibilityStatus; reason: string; changedAtUtc: string }
export interface DonationRecord { id: string; donationDate: string; units: number; location: string; notes?: string }
export type CampSlotStatus = 'Available' | 'Booked' | 'CheckedIn' | 'NoShow' | 'Cancelled'
export interface CampSlot { id: string; campId: string; donorId?: string | null; slotTimeUtc: string; status: CampSlotStatus; version: number }
export interface CampRosterEntry { slotId: string; slotTimeUtc: string; status: CampSlotStatus; donorId: string; donorEmail: string; bloodType: BloodType }
export interface AttendanceReport { campId: string; capacity: number; booked: number; checkedIn: number; noShows: number; cancelled: number; attendanceRate: number }
export interface ProblemDetails { title?: string; detail?: string; status?: number; errors?: Record<string, string[]>; correlationId?: string }
