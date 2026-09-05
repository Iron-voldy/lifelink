namespace LifeLink.Domain.Enums;

public enum UserRole { Donor, HospitalRequester, BloodBankAdmin, CampCoordinator }
public enum BloodType { APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative }
public enum EligibilityStatus { PendingVerification, Eligible, TemporarilyIneligible, PermanentlyIneligible }
public enum VerificationStatus { Pending, Verified, Rejected, Suspended }
public enum RequestUrgency { Routine, Urgent, Critical }
public enum BloodRequestStatus { Submitted, UnderReview, PendingApproval, Approved, Dispatched, Fulfilled, Escalated, ClosedUnfulfilled }
public enum InventoryLotStatus { Available, Reserved, Dispatched, Expired, Quarantined }
public enum ReservationStatus { Active, Released, Dispatched, Expired }
public enum CampStatus { Draft, Scheduled, InProgress, Closed, Cancelled }
public enum CampSlotStatus { Available, Booked, CheckedIn, NoShow, Cancelled }
public enum NotificationStatus { Pending, Sent, Failed, Delivered }
public enum WorkflowStatus { Pending, Running, PendingApproval, Approved, Rejected, Revising, Completed, EscalationRequired, Failed }
public enum AgentStepStatus { Pending, Running, Completed, Failed, Skipped }
public enum ApprovalDecision { Approved, Rejected, RevisionRequested }
