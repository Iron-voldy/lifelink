using System.Text.Json;
using LifeLink.Application.Donors;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Infrastructure.Donors;

public sealed class DonorService(LifeLinkDbContext db, TimeProvider timeProvider) : IDonorService
{
    private const int MinimumAge = 18;
    private const int MaximumPlausibleAge = 120;
    private const int CooldownDays = 90;

    public async Task<DonorResult<DonorView>> CreateAsync(Guid userId, DonorProfileInput input, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || user.Role != UserRole.Donor) return DonorResult<DonorView>.Failure("invalid_donor_user", "A donor user account is required.");
        if (await db.Donors.AnyAsync(x => x.UserId == userId, ct)) return DonorResult<DonorView>.Failure("profile_exists", "A donor profile already exists.");
        var validation = Validate(input);
        if (validation is not null) return DonorResult<DonorView>.Failure("invalid_profile", validation);
        var donor = new Donor(userId, input.BloodType, input.DateOfBirth, input.Address, input.Latitude, input.Longitude, SerializeFlags(input.MedicalFlags));
        db.Donors.Add(donor);
        await db.SaveChangesAsync(ct);
        return DonorResult<DonorView>.Success(Map(donor, user.Email));
    }

    public async Task<DonorResult<DonorView>> GetAsync(Guid donorId, CancellationToken ct)
    {
        var row = await db.Donors.Where(x => x.Id == donorId).Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d, u.Email }).SingleOrDefaultAsync(ct);
        return row is null ? DonorResult<DonorView>.Failure("donor_not_found", "Donor was not found.") : DonorResult<DonorView>.Success(Map(row.d, row.Email));
    }

    public async Task<DonorResult<DonorView>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.Donors.Where(x => x.UserId == userId).Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d, u.Email }).SingleOrDefaultAsync(ct);
        return row is null ? DonorResult<DonorView>.Failure("donor_not_found", "Donor profile was not found.") : DonorResult<DonorView>.Success(Map(row.d, row.Email));
    }

    public async Task<PagedResult<DonorView>> ListAsync(DonorQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page); var size = Math.Clamp(query.PageSize, 1, 100);
        var rows = db.Donors.AsNoTracking().Join(db.Users, d => d.UserId, u => u.Id, (d, u) => new { d, u.Email });
        if (!query.IncludeInactive) rows = rows.Where(x => x.d.IsActive);
        if (query.BloodType is not null) rows = rows.Where(x => x.d.BloodType == query.BloodType);
        if (query.EligibilityStatus is not null) rows = rows.Where(x => x.d.EligibilityStatus == query.EligibilityStatus);
        if (!string.IsNullOrWhiteSpace(query.Search)) { var search = query.Search.Trim().ToLower(); rows = rows.Where(x => x.Email.ToLower().Contains(search) || x.d.Address.ToLower().Contains(search)); }
        rows = (query.SortBy.ToLowerInvariant(), query.Descending) switch
        {
            ("email", false) => rows.OrderBy(x => x.Email), ("email", true) => rows.OrderByDescending(x => x.Email),
            ("bloodtype", false) => rows.OrderBy(x => x.d.BloodType), ("bloodtype", true) => rows.OrderByDescending(x => x.d.BloodType),
            ("status", false) => rows.OrderBy(x => x.d.EligibilityStatus), ("status", true) => rows.OrderByDescending(x => x.d.EligibilityStatus),
            (_, true) => rows.OrderByDescending(x => x.d.CreatedAtUtc), _ => rows.OrderBy(x => x.d.CreatedAtUtc)
        };
        var count = await rows.CountAsync(ct);
        var data = await rows.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<DonorView>(data.Select(x => Map(x.d, x.Email)).ToList(), page, size, count);
    }

    public async Task<DonorResult<DonorView>> UpdateAsync(Guid donorId, DonorProfileInput input, CancellationToken ct)
    {
        var donor = await db.Donors.SingleOrDefaultAsync(x => x.Id == donorId, ct);
        if (donor is null) return DonorResult<DonorView>.Failure("donor_not_found", "Donor was not found.");
        var validation = Validate(input); if (validation is not null) return DonorResult<DonorView>.Failure("invalid_profile", validation);
        var flagsJson = SerializeFlags(input.MedicalFlags);
        // Blood type, age and medical notes drive matching and eligibility, so a stale "Eligible" must not survive a change to them.
        var needsRecheck = donor.EligibilityStatus == EligibilityStatus.Eligible && (donor.BloodType != input.BloodType || donor.DateOfBirth != input.DateOfBirth || !DeserializeFlags(donor.MedicalFlagsJson).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(DeserializeFlags(flagsJson)));
        var previous = donor.EligibilityStatus;
        donor.Update(input.BloodType, input.DateOfBirth, input.Address, input.Latitude, input.Longitude, flagsJson);
        if (needsRecheck)
        {
            donor.ApplyEligibility(EligibilityStatus.PendingVerification);
            db.DonorEligibilityHistory.Add(new DonorEligibilityHistory(donor.Id, previous, EligibilityStatus.PendingVerification, "Blood type, date of birth or medical notes changed; staff must re-verify eligibility.", null));
        }
        await db.SaveChangesAsync(ct);
        var email = await db.Users.Where(x => x.Id == donor.UserId).Select(x => x.Email).SingleAsync(ct);
        return DonorResult<DonorView>.Success(Map(donor, email));
    }

    public async Task<DonorResult<bool>> DeactivateAsync(Guid donorId, CancellationToken ct)
    {
        var donor = await db.Donors.SingleOrDefaultAsync(x => x.Id == donorId, ct);
        if (donor is null) return DonorResult<bool>.Failure("donor_not_found", "Donor was not found.");
        donor.Deactivate(); await db.SaveChangesAsync(ct); return DonorResult<bool>.Success(true);
    }

    public async Task<DonorResult<EligibilityEvaluation>> EvaluateEligibilityAsync(Guid donorId, Guid changedByUserId, bool verifiedByStaff, CancellationToken ct)
    {
        var donor = await db.Donors.SingleOrDefaultAsync(x => x.Id == donorId, ct);
        if (donor is null) return DonorResult<EligibilityEvaluation>.Failure("donor_not_found", "Donor was not found.");
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime); var reasons = new List<string>(); var permanent = false;
        var age = today.Year - donor.DateOfBirth.Year - (today < donor.DateOfBirth.AddYears(today.Year - donor.DateOfBirth.Year) ? 1 : 0);
        if (age < MinimumAge) reasons.Add($"Donor must be at least {MinimumAge} years old.");
        if (age > 60) { reasons.Add("Donor is above the maximum donation age of 60."); permanent = true; }
        if (donor.LastDonationDate is not null && donor.LastDonationDate.Value.AddDays(CooldownDays) > today) reasons.Add($"A {CooldownDays}-day interval is required after the previous donation.");
        var flags = DeserializeFlags(donor.MedicalFlagsJson); if (flags.Count > 0) reasons.Add("Medical review required: " + string.Join(", ", flags));
        var previous = donor.EligibilityStatus; var status = reasons.Count == 0 ? EligibilityStatus.Eligible : permanent ? EligibilityStatus.PermanentlyIneligible : EligibilityStatus.TemporarilyIneligible;
        var reason = reasons.Count == 0 ? "All deterministic eligibility checks passed." : string.Join(" ", reasons);
        // The automatic check may only tighten a donor's status on its own: it never lifts a permanent deferral,
        // and a donor's self-check cannot grant "Eligible" - blood bank staff have to verify that.
        if (previous == EligibilityStatus.PermanentlyIneligible && status != EligibilityStatus.PermanentlyIneligible)
        {
            status = EligibilityStatus.PermanentlyIneligible; reason = "Permanently ineligible donors can only be reinstated by blood bank staff."; reasons.Add(reason);
        }
        else if (status == EligibilityStatus.Eligible && previous != EligibilityStatus.Eligible && !verifiedByStaff)
        {
            status = EligibilityStatus.PendingVerification; reason = "All automatic checks passed; blood bank staff must verify the donor before they can book."; reasons.Add(reason);
        }
        donor.ApplyEligibility(status);
        db.DonorEligibilityHistory.Add(new DonorEligibilityHistory(donor.Id, previous, status, reason, changedByUserId)); await db.SaveChangesAsync(ct);
        return DonorResult<EligibilityEvaluation>.Success(new(donor.Id, previous, status, reasons, timeProvider.GetUtcNow()));
    }

    public async Task<DonorResult<IReadOnlyList<EligibilityHistoryView>>> GetEligibilityHistoryAsync(Guid donorId, CancellationToken ct)
    {
        if (!await db.Donors.AnyAsync(x => x.Id == donorId, ct)) return DonorResult<IReadOnlyList<EligibilityHistoryView>>.Failure("donor_not_found", "Donor was not found.");
        var data = await db.DonorEligibilityHistory.AsNoTracking().Where(x => x.DonorId == donorId).OrderByDescending(x => x.CreatedAtUtc).Select(x => new EligibilityHistoryView(x.Id, x.PreviousStatus, x.NewStatus, x.Reason, x.ChangedByUserId, x.CreatedAtUtc)).ToListAsync(ct);
        return DonorResult<IReadOnlyList<EligibilityHistoryView>>.Success(data);
    }

    public async Task<DonorResult<DonationRecordView>> RecordDonationAsync(Guid donorId, DonationRecordInput input, CancellationToken ct)
    {
        var donor = await db.Donors.SingleOrDefaultAsync(x => x.Id == donorId, ct);
        if (donor is null) return DonorResult<DonationRecordView>.Failure("donor_not_found", "Donor was not found.");
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (input.DonationDate > today || input.Units is < 1 or > 4 || string.IsNullOrWhiteSpace(input.Location) || input.Location.Length > 500 || input.Notes?.Length > 1000) return DonorResult<DonationRecordView>.Failure("invalid_donation", "Donation date, units, location, or notes are invalid.");
        if (input.DonationDate < donor.DateOfBirth.AddYears(16)) return DonorResult<DonationRecordView>.Failure("invalid_donation", "Donation date cannot be before the donor was old enough to donate.");
        if (await db.DonationRecords.AnyAsync(x => x.DonorId == donorId && x.DonationDate == input.DonationDate, ct)) return DonorResult<DonationRecordView>.Failure("donation_exists", "A donation is already recorded for this donor on that date.");
        var record = new DonationRecord(donorId, input.DonationDate, input.Units, input.Location.Trim(), string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim());
        db.DonationRecords.Add(record);
        // Back-filling an older donation must not move the last-donation date backwards (which would shorten the cooldown).
        if (donor.LastDonationDate is null || input.DonationDate > donor.LastDonationDate) donor.RecordDonation(input.DonationDate);
        if (donor.LastDonationDate!.Value.AddDays(CooldownDays) > today && donor.EligibilityStatus is EligibilityStatus.Eligible or EligibilityStatus.PendingVerification)
        {
            var previous = donor.EligibilityStatus;
            donor.ApplyEligibility(EligibilityStatus.TemporarilyIneligible);
            db.DonorEligibilityHistory.Add(new DonorEligibilityHistory(donor.Id, previous, EligibilityStatus.TemporarilyIneligible, $"Donation recorded; {CooldownDays}-day cooldown started.", null));
        }
        await db.SaveChangesAsync(ct);
        return DonorResult<DonationRecordView>.Success(Map(record));
    }

    public async Task<DonorResult<IReadOnlyList<DonationRecordView>>> GetDonationHistoryAsync(Guid donorId, CancellationToken ct)
    {
        if (!await db.Donors.AnyAsync(x => x.Id == donorId, ct)) return DonorResult<IReadOnlyList<DonationRecordView>>.Failure("donor_not_found", "Donor was not found.");
        var data = await db.DonationRecords.AsNoTracking().Where(x => x.DonorId == donorId).OrderByDescending(x => x.DonationDate).ToListAsync(ct);
        return DonorResult<IReadOnlyList<DonationRecordView>>.Success(data.Select(Map).ToList());
    }

    private string? Validate(DonorProfileInput input)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (input.DateOfBirth > today) return "Date of birth cannot be in the future.";
        if (input.DateOfBirth > today.AddYears(-MinimumAge)) return $"Donors must be at least {MinimumAge} years old.";
        if (input.DateOfBirth < today.AddYears(-MaximumPlausibleAge)) return $"Date of birth cannot be more than {MaximumPlausibleAge} years ago.";
        if (string.IsNullOrWhiteSpace(input.Address) || input.Address.Length > 500) return "Address is required and must be at most 500 characters.";
        if (input.Latitude is < -90 or > 90 || input.Longitude is < -180 or > 180) return "Coordinates are outside valid ranges.";
        if (input.MedicalFlags.Count > 20 || input.MedicalFlags.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100)) return "Medical flags are invalid.";
        return null;
    }
    private static string SerializeFlags(IReadOnlyList<string> flags) => JsonSerializer.Serialize(flags.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
    private static IReadOnlyList<string> DeserializeFlags(string json) => JsonSerializer.Deserialize<List<string>>(json) ?? [];
    private static DonorView Map(Donor d, string email) => new(d.Id, d.UserId, email, d.BloodType, d.DateOfBirth, d.LastDonationDate, d.EligibilityStatus, d.Address, d.Latitude, d.Longitude, DeserializeFlags(d.MedicalFlagsJson), d.IsActive, d.CreatedAtUtc, d.UpdatedAtUtc);
    private static DonationRecordView Map(DonationRecord x) => new(x.Id, x.DonationDate, x.Units, x.Location, x.Notes, x.CreatedAtUtc);
}
