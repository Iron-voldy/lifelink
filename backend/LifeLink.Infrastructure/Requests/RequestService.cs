using System.Text.RegularExpressions;
using LifeLink.Application.Donors;
using LifeLink.Application.Requests;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Infrastructure.Requests;

public sealed class RequestService(LifeLinkDbContext db, TimeProvider timeProvider, IRequestWorkflowTrigger workflowTrigger) : IRequestService
{
    private static readonly IReadOnlyDictionary<BloodRequestStatus, BloodRequestStatus[]> AllowedTransitions = new Dictionary<BloodRequestStatus, BloodRequestStatus[]>
    {
        [BloodRequestStatus.Submitted] = [BloodRequestStatus.UnderReview, BloodRequestStatus.Escalated, BloodRequestStatus.ClosedUnfulfilled],
        [BloodRequestStatus.UnderReview] = [BloodRequestStatus.PendingApproval, BloodRequestStatus.Escalated, BloodRequestStatus.ClosedUnfulfilled],
        [BloodRequestStatus.PendingApproval] = [BloodRequestStatus.Approved, BloodRequestStatus.ClosedUnfulfilled, BloodRequestStatus.Escalated],
        [BloodRequestStatus.Approved] = [BloodRequestStatus.Dispatched, BloodRequestStatus.ClosedUnfulfilled],
        [BloodRequestStatus.Dispatched] = [BloodRequestStatus.Fulfilled, BloodRequestStatus.ClosedUnfulfilled],
        [BloodRequestStatus.Escalated] = [BloodRequestStatus.UnderReview, BloodRequestStatus.PendingApproval, BloodRequestStatus.ClosedUnfulfilled]
    };

    // Before human approval a request can still be corrected, escalated or withdrawn by the hospital; after it, stock is committed.
    private static readonly BloodRequestStatus[] PreApprovalStatuses = [BloodRequestStatus.Submitted, BloodRequestStatus.UnderReview, BloodRequestStatus.PendingApproval, BloodRequestStatus.Escalated];
    // Blood components keep for weeks, so a required-by time further out than this is a data-entry mistake.
    private static readonly TimeSpan MaxRequestHorizon = TimeSpan.FromDays(30);
    private static readonly Regex RegistrationFormat = new("^[A-Z0-9][A-Z0-9/.-]{1,98}[A-Z0-9]$", RegexOptions.CultureInvariant);

    public async Task<RequestResult<HospitalView>> RegisterHospitalAsync(Guid staffUserId, HospitalRegistrationInput input, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == staffUserId, ct);
        if (user is null || user.Role != UserRole.HospitalRequester) return RequestResult<HospitalView>.Failure("invalid_hospital_user", "A hospital requester account is required.");
        if (await db.HospitalStaff.AnyAsync(x => x.UserId == staffUserId, ct)) return RequestResult<HospitalView>.Failure("staff_profile_exists", "This account is already linked to a hospital.");
        input = input with { Name = input.Name?.Trim() ?? string.Empty, RegistrationNumber = input.RegistrationNumber?.Trim().ToUpperInvariant() ?? string.Empty, Address = input.Address?.Trim() ?? string.Empty, Position = input.Position?.Trim() ?? string.Empty };
        var validation = ValidateHospital(input); if (validation is not null) return RequestResult<HospitalView>.Failure("invalid_hospital", validation);
        var registration = input.RegistrationNumber;
        if (await db.Hospitals.AnyAsync(x => x.RegistrationNumber == registration, ct)) return RequestResult<HospitalView>.Failure("registration_exists", "Hospital registration number already exists.");
        var hospital = new Hospital(input.Name, registration, input.Address, input.Latitude, input.Longitude);
        db.Hospitals.Add(hospital); db.HospitalStaff.Add(new HospitalStaff(staffUserId, hospital.Id, input.Position)); await db.SaveChangesAsync(ct);
        return RequestResult<HospitalView>.Success(Map(hospital));
    }

    public async Task<RequestResult<HospitalView>> SetHospitalVerificationAsync(Guid hospitalId, VerificationStatus status, CancellationToken ct)
    {
        var hospital = await db.Hospitals.SingleOrDefaultAsync(x => x.Id == hospitalId, ct);
        if (hospital is null) return RequestResult<HospitalView>.Failure("hospital_not_found", "Hospital was not found.");
        hospital.SetVerification(status); await db.SaveChangesAsync(ct); return RequestResult<HospitalView>.Success(Map(hospital));
    }

    public async Task<PagedResult<HospitalView>> ListHospitalsAsync(VerificationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var query = db.Hospitals.AsNoTracking();
        if (status is not null) query = query.Where(x => x.VerificationStatus == status);
        var count = await query.CountAsync(ct); var data = await query.OrderBy(x => x.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(data.Select(Map).ToList(), page, pageSize, count);
    }

    public async Task<RequestResult<BloodRequestView>> CreateAsync(Guid staffUserId, RequestInput input, CancellationToken ct)
    {
        var staff = await db.HospitalStaff.SingleOrDefaultAsync(x => x.UserId == staffUserId, ct);
        if (staff is null) return RequestResult<BloodRequestView>.Failure("hospital_profile_required", "Register a hospital profile first.");
        var hospital = await db.Hospitals.SingleAsync(x => x.Id == staff.HospitalId, ct);
        if (hospital.VerificationStatus != VerificationStatus.Verified) return RequestResult<BloodRequestView>.Failure("hospital_not_verified", "Hospital must be verified before submitting requests.");
        input = Normalize(input); var validation = ValidateRequest(input); if (validation is not null) return RequestResult<BloodRequestView>.Failure("invalid_request", validation);
        var urgency = CalculateUrgency(input, timeProvider.GetUtcNow());
        var request = new BloodRequest(hospital.Id, staffUserId, input.BloodType, input.QuantityUnits, urgency, input.Notes, input.RequiredByUtc);
        db.BloodRequests.Add(request); db.RequestStatusHistory.Add(new RequestStatusHistory(request.Id, BloodRequestStatus.Submitted, BloodRequestStatus.Submitted, staffUserId, $"Request submitted with deterministic urgency {urgency}."));
        await db.SaveChangesAsync(ct); await workflowTrigger.TriggerAsync(request.Id, "request-created", ct);
        return RequestResult<BloodRequestView>.Success(Map(request, hospital.Name));
    }

    public async Task<RequestResult<BloodRequestView>> GetAsync(Guid requestId, CancellationToken ct)
    {
        var request = await db.BloodRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return RequestResult<BloodRequestView>.Failure("request_not_found", "Blood request was not found.");
        var hospitalName = await db.Hospitals.Where(x => x.Id == request.HospitalId).Select(x => x.Name).SingleAsync(ct);
        return RequestResult<BloodRequestView>.Success(Map(request, hospitalName));
    }

    public async Task<PagedResult<BloodRequestView>> ListAsync(RequestQuery input, CancellationToken ct)
    {
        var page = Math.Max(1, input.Page); var size = Math.Clamp(input.PageSize, 1, 100); var query = db.BloodRequests.AsNoTracking();
        if (input.HospitalId is not null) query = query.Where(x => x.HospitalId == input.HospitalId);
        if (input.Status is not null) query = query.Where(x => x.Status == input.Status);
        if (input.Urgency is not null) query = query.Where(x => x.Urgency == input.Urgency);
        if (input.BloodType is not null) query = query.Where(x => x.BloodType == input.BloodType);
        query = (input.SortBy.ToLowerInvariant(), input.Descending) switch
        {
            ("requiredby", false) => query.OrderBy(x => x.RequiredByUtc), ("requiredby", true) => query.OrderByDescending(x => x.RequiredByUtc),
            ("urgency", false) => query.OrderBy(x => x.Urgency), ("urgency", true) => query.OrderByDescending(x => x.Urgency),
            (_, true) => query.OrderByDescending(x => x.CreatedAtUtc), _ => query.OrderBy(x => x.CreatedAtUtc)
        };
        var count = await query.CountAsync(ct); var data = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var hospitalIds = data.Select(x => x.HospitalId).Distinct().ToList();
        var names = await db.Hospitals.Where(x => hospitalIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        return new(data.Select(x => Map(x, names[x.HospitalId])).ToList(), page, size, count);
    }

    public async Task<RequestResult<BloodRequestView>> UpdateAsync(Guid requestId, RequestInput input, Guid actorUserId, CancellationToken ct)
    {
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return RequestResult<BloodRequestView>.Failure("request_not_found", "Blood request was not found.");
        if (!PreApprovalStatuses.Contains(request.Status)) return RequestResult<BloodRequestView>.Failure("request_not_editable", "Only requests that have not been approved yet can be edited.");
        if (!await IsHospitalVerifiedAsync(request.HospitalId, ct)) return RequestResult<BloodRequestView>.Failure("hospital_not_verified", "Hospital must be verified before changing requests.");
        if (await db.AgentWorkflowExecutions.AnyAsync(x => x.BloodRequestId == requestId && x.Status == WorkflowStatus.Running, ct)) return RequestResult<BloodRequestView>.Failure("request_not_editable", "The request is being analysed right now. Try again in a moment.");
        input = Normalize(input); var validation = ValidateRequest(input); if (validation is not null) return RequestResult<BloodRequestView>.Failure("invalid_request", validation);
        var urgency = CalculateUrgency(input, timeProvider.GetUtcNow());
        // An escalation is a clinical decision; editing the details must not silently undo it.
        if (request.Status == BloodRequestStatus.Escalated && request.Urgency > urgency) urgency = request.Urgency;
        var reanalyse = request.Status != BloodRequestStatus.Submitted;
        request.Update(input.BloodType, input.QuantityUnits, urgency, input.Notes, input.RequiredByUtc);
        db.RequestStatusHistory.Add(new RequestStatusHistory(request.Id, request.Status, request.Status, actorUserId, $"Request details updated; urgency recalculated as {urgency}."));
        // A proposal awaiting approval was made for the old details; supersede it so the agents re-plan for the new ones.
        if (reanalyse) foreach (var stale in await db.AgentWorkflowExecutions.Where(x => x.BloodRequestId == requestId && x.Status == WorkflowStatus.PendingApproval).ToListAsync(ct)) stale.SetStatus(WorkflowStatus.Revising);
        await db.SaveChangesAsync(ct); if (reanalyse) await workflowTrigger.TriggerAsync(request.Id, "request-updated", ct);
        var name = await db.Hospitals.Where(x => x.Id == request.HospitalId).Select(x => x.Name).SingleAsync(ct); return RequestResult<BloodRequestView>.Success(Map(request, name));
    }

    public async Task<RequestResult<BloodRequestView>> TransitionAsync(Guid requestId, BloodRequestStatus status, Guid actorUserId, string? reason, CancellationToken ct)
    {
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return RequestResult<BloodRequestView>.Failure("request_not_found", "Blood request was not found.");
        if (!AllowedTransitions.TryGetValue(request.Status, out var allowed) || !allowed.Contains(status)) return RequestResult<BloodRequestView>.Failure("invalid_transition", $"Cannot transition from {request.Status} to {status}.");
        var pending = await db.AgentWorkflowExecutions.Where(x => x.BloodRequestId == requestId && x.Status == WorkflowStatus.PendingApproval).ToListAsync(ct);
        // Approving by hand while an agent proposal is still open would let that proposal reserve and dispatch the same units again later.
        if (status == BloodRequestStatus.Approved && pending.Count > 0) return RequestResult<BloodRequestView>.Failure("workflow_pending", "An agent proposal for this request is awaiting approval. Approve, reject or revise it in Agent workflows instead.");
        var previous = request.Status; request.TransitionTo(status); db.RequestStatusHistory.Add(new RequestStatusHistory(request.Id, previous, status, actorUserId, reason));
        // A closed request must not stay in the approval queue, where approving it would still reserve and dispatch blood.
        if (status == BloodRequestStatus.ClosedUnfulfilled)
        {
            foreach (var execution in pending) execution.Finish(WorkflowStatus.Rejected, execution.FinalOutcomeJson, timeProvider.GetUtcNow());
            // Units held for a closed request go back on the shelf in the same save as the closure (mirrors InventoryService.ReleaseAsync).
            foreach (var reservation in await db.InventoryReservations.Where(x => x.BloodRequestId == requestId && x.Status == ReservationStatus.Active).ToListAsync(ct))
            {
                reservation.Release("Blood request closed."); (await db.InventoryLots.SingleAsync(x => x.Id == reservation.InventoryLotId, ct)).Release(reservation.Units);
            }
        }
        await db.SaveChangesAsync(ct);
        var name = await db.Hospitals.Where(x => x.Id == request.HospitalId).Select(x => x.Name).SingleAsync(ct); return RequestResult<BloodRequestView>.Success(Map(request, name));
    }

    public async Task<RequestResult<BloodRequestView>> EscalateAsync(Guid requestId, Guid actorUserId, string? reason, CancellationToken ct)
    {
        var request = await db.BloodRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return RequestResult<BloodRequestView>.Failure("request_not_found", "Blood request was not found.");
        if (request.Status is BloodRequestStatus.Fulfilled or BloodRequestStatus.ClosedUnfulfilled) return RequestResult<BloodRequestView>.Failure("request_closed", "Closed requests cannot be escalated.");
        if (!PreApprovalStatuses.Contains(request.Status)) return RequestResult<BloodRequestView>.Failure("request_not_escalatable", $"A request that is already {request.Status} cannot be escalated.");
        if (request.Status == BloodRequestStatus.Escalated && request.Urgency == RequestUrgency.Critical) return RequestResult<BloodRequestView>.Failure("request_not_escalatable", "This request is already escalated at critical urgency.");
        if (!await IsHospitalVerifiedAsync(request.HospitalId, ct)) return RequestResult<BloodRequestView>.Failure("hospital_not_verified", "Hospital must be verified before escalating requests.");
        var previous = request.Status; var urgency = request.Urgency switch { RequestUrgency.Routine => RequestUrgency.Urgent, _ => RequestUrgency.Critical };
        request.Escalate(urgency); db.RequestStatusHistory.Add(new RequestStatusHistory(request.Id, previous, BloodRequestStatus.Escalated, actorUserId, reason ?? $"Escalated to {urgency}.")); await db.SaveChangesAsync(ct); await workflowTrigger.TriggerAsync(request.Id, "request-escalated", ct);
        var name = await db.Hospitals.Where(x => x.Id == request.HospitalId).Select(x => x.Name).SingleAsync(ct); return RequestResult<BloodRequestView>.Success(Map(request, name));
    }

    public async Task<RequestResult<BloodRequestView>> CancelAsync(Guid requestId, Guid actorUserId, CancellationToken ct)
    {
        var request = await db.BloodRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null) return RequestResult<BloodRequestView>.Failure("request_not_found", "Blood request was not found.");
        if (request.Status is BloodRequestStatus.Fulfilled or BloodRequestStatus.ClosedUnfulfilled) return RequestResult<BloodRequestView>.Failure("request_closed", "This request is already closed.");
        if (!PreApprovalStatuses.Contains(request.Status)) return RequestResult<BloodRequestView>.Failure("request_not_cancellable", $"A request that is already {request.Status} cannot be withdrawn; contact the blood bank.");
        return await TransitionAsync(requestId, BloodRequestStatus.ClosedUnfulfilled, actorUserId, "Closed by hospital requester.", ct);
    }

    public async Task<RequestResult<IReadOnlyList<RequestHistoryView>>> HistoryAsync(Guid requestId, CancellationToken ct)
    {
        if (!await db.BloodRequests.AnyAsync(x => x.Id == requestId, ct)) return RequestResult<IReadOnlyList<RequestHistoryView>>.Failure("request_not_found", "Blood request was not found.");
        var data = await db.RequestStatusHistory.AsNoTracking().Where(x => x.BloodRequestId == requestId).OrderBy(x => x.CreatedAtUtc).Select(x => new RequestHistoryView(x.Id, x.PreviousStatus, x.NewStatus, x.ChangedByUserId, x.Reason, x.CreatedAtUtc)).ToListAsync(ct);
        return RequestResult<IReadOnlyList<RequestHistoryView>>.Success(data);
    }

    public async Task<RequestSummary> SummaryAsync(Guid? hospitalId, CancellationToken ct)
    {
        var query = db.BloodRequests.AsNoTracking(); if (hospitalId is not null) query = query.Where(x => x.HospitalId == hospitalId);
        var rows = await query.ToListAsync(ct); var terminal = new[] { BloodRequestStatus.Fulfilled, BloodRequestStatus.ClosedUnfulfilled };
        return new(rows.Count, rows.Count(x => !terminal.Contains(x.Status)), rows.Count(x => x.Urgency == RequestUrgency.Critical && !terminal.Contains(x.Status)), rows.Count(x => x.Status == BloodRequestStatus.Fulfilled), rows.Count(x => x.Status == BloodRequestStatus.ClosedUnfulfilled), rows.GroupBy(x => x.BloodType).ToDictionary(x => x.Key, x => x.Sum(y => y.QuantityUnits)));
    }

    public async Task<RequestResult<Guid>> GetHospitalIdForStaffAsync(Guid userId, CancellationToken ct)
    {
        var id = await db.HospitalStaff.Where(x => x.UserId == userId).Select(x => (Guid?)x.HospitalId).SingleOrDefaultAsync(ct);
        return id is null ? RequestResult<Guid>.Failure("hospital_profile_required", "Hospital profile is required.") : RequestResult<Guid>.Success(id.Value);
    }

    public async Task<RequestResult<HospitalView>> GetHospitalForStaffAsync(Guid userId, CancellationToken ct)
    {
        var hospital = await db.HospitalStaff.AsNoTracking().Where(x => x.UserId == userId).Join(db.Hospitals.AsNoTracking(), staff => staff.HospitalId, h => h.Id, (_, h) => h).SingleOrDefaultAsync(ct);
        return hospital is null ? RequestResult<HospitalView>.Failure("hospital_profile_required", "This account is not linked to a hospital yet.") : RequestResult<HospitalView>.Success(Map(hospital));
    }

    private Task<bool> IsHospitalVerifiedAsync(Guid hospitalId, CancellationToken ct) => db.Hospitals.AnyAsync(x => x.Id == hospitalId && x.VerificationStatus == VerificationStatus.Verified, ct);
    // Npgsql only stores UTC offsets, so a client-local offset (e.g. +05:30) is converted instead of failing the save.
    private static RequestInput Normalize(RequestInput input) => input with { RequiredByUtc = input.RequiredByUtc.ToUniversalTime(), Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim() };

    private static RequestUrgency CalculateUrgency(RequestInput input, DateTimeOffset now)
    {
        var remaining = input.RequiredByUtc - now;
        var calculated = remaining <= TimeSpan.FromHours(2) || input.QuantityUnits >= 6 ? RequestUrgency.Critical : remaining <= TimeSpan.FromHours(12) || input.QuantityUnits >= 3 ? RequestUrgency.Urgent : RequestUrgency.Routine;
        return (RequestUrgency)Math.Max((int)calculated, (int)input.RequestedUrgency);
    }
    private DateTimeOffset Now => timeProvider.GetUtcNow();
    private string? ValidateRequest(RequestInput input) => !Enum.IsDefined(input.BloodType) || !Enum.IsDefined(input.RequestedUrgency) ? "Blood type and urgency must be allowed values." : input.QuantityUnits is < 1 or > 100 ? "Quantity must be between 1 and 100 units." : input.RequiredByUtc <= Now ? "Required-by time must be in the future." : input.RequiredByUtc > Now + MaxRequestHorizon ? $"Required-by time must be within {MaxRequestHorizon.TotalDays:0} days." : input.Notes?.Length > 2000 ? "Notes must be at most 2000 characters." : null;
    private static string? ValidateHospital(HospitalRegistrationInput x) => x.Name.Length is < 2 or > 250 || !x.Name.Any(char.IsLetter) ? "Hospital name must be 2-250 characters and contain letters." : !RegistrationFormat.IsMatch(x.RegistrationNumber) ? "Registration number must be 3-100 characters of letters, digits, '-', '/' or '.', starting and ending with a letter or digit." : x.Address.Length is < 3 or > 500 ? "Address must be 3-500 characters." : x.Position.Length is < 2 or > 150 ? "Staff position must be 2-150 characters." : (x.Latitude is null) != (x.Longitude is null) ? "Provide both latitude and longitude, or neither." : x.Latitude is < -90 or > 90 || x.Longitude is < -180 or > 180 ? "Coordinates are outside valid ranges." : null;
    private static HospitalView Map(Hospital x) => new(x.Id, x.Name, x.RegistrationNumber, x.VerificationStatus, x.Address, x.Latitude, x.Longitude, x.CreatedAtUtc);
    private static BloodRequestView Map(BloodRequest x, string hospitalName) => new(x.Id, x.HospitalId, hospitalName, x.RequestedByUserId, x.BloodType, x.QuantityUnits, x.Urgency, x.Status, x.Notes, x.RequiredByUtc, x.CreatedAtUtc, x.UpdatedAtUtc);
}
