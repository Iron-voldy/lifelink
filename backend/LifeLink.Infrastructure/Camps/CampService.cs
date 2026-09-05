using LifeLink.Application.Camps;
using LifeLink.Application.Donors;
using LifeLink.Application.Notifications;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using LifeLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Infrastructure.Camps;

public sealed class CampService(LifeLinkDbContext db, TimeProvider timeProvider, INotificationService notifications) : ICampService
{
    private static readonly IReadOnlyDictionary<CampStatus, CampStatus[]> Transitions = new Dictionary<CampStatus, CampStatus[]>
    {
        [CampStatus.Draft] = [CampStatus.Scheduled, CampStatus.Cancelled],
        [CampStatus.Scheduled] = [CampStatus.InProgress, CampStatus.Cancelled],
        [CampStatus.InProgress] = [CampStatus.Closed]
    };
    /// <summary>Coordinators may open a camp shortly before the first slot, but not days early.</summary>
    private static readonly TimeSpan EarlyStartWindow = TimeSpan.FromHours(1);

    public async Task<CampResult<CampView>> CreateAsync(Guid organizerUserId, CampInput input, CancellationToken ct)
    {
        input = ToUtc(input);
        var validation = Validate(input); if (validation is not null) return CampResult<CampView>.Failure("invalid_camp", validation);
        if (!await db.Users.AnyAsync(x => x.Id == organizerUserId && x.IsActive, ct)) return CampResult<CampView>.Failure("organizer_not_found", "Organizer was not found.");
        var camp = new DonationCamp(organizerUserId, input.Name, input.Location, input.Latitude, input.Longitude, input.StartsAtUtc, input.EndsAtUtc, input.Capacity); db.DonationCamps.Add(camp);
        foreach (var time in SlotTimes(input)) db.CampSlots.Add(new CampSlot(camp.Id, time));
        await db.SaveChangesAsync(ct); return CampResult<CampView>.Success(await Map(camp, ct));
    }

    public async Task<CampResult<CampView>> GetAsync(Guid id, CancellationToken ct)
    {
        var camp = await db.DonationCamps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return camp is null ? CampResult<CampView>.Failure("camp_not_found", "Donation camp was not found.") : CampResult<CampView>.Success(await Map(camp, ct));
    }

    public async Task<PagedResult<CampView>> ListAsync(CampQuery input, CancellationToken ct)
    {
        var page = Math.Max(1, input.Page); var size = Math.Clamp(input.PageSize, 1, 100); var query = db.DonationCamps.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(input.Search)) { var search = input.Search.Trim().ToLower(); query = query.Where(x => x.Name.ToLower().Contains(search) || x.Location.ToLower().Contains(search)); }
        if (input.PublicOnly) query = query.Where(x => x.Status != CampStatus.Draft);
        if (input.Status is not null) query = query.Where(x => x.Status == input.Status); if (input.FromUtc is { } from) { from = from.ToUniversalTime(); query = query.Where(x => x.StartsAtUtc >= from); } if (input.ToUtc is { } to) { to = to.ToUniversalTime(); query = query.Where(x => x.StartsAtUtc <= to); }
        var count = await query.CountAsync(ct); var camps = await query.OrderBy(x => x.StartsAtUtc).Skip((page - 1) * size).Take(size).ToListAsync(ct); var views = await MapMany(camps, ct); return new(views, page, size, count);
    }

    public async Task<CampResult<CampView>> UpdateAsync(Guid id, CampInput input, CancellationToken ct)
    {
        var camp = await db.DonationCamps.SingleOrDefaultAsync(x => x.Id == id, ct); if (camp is null) return CampResult<CampView>.Failure("camp_not_found", "Donation camp was not found.");
        input = ToUtc(input); var validation = Validate(input); if (validation is not null) return CampResult<CampView>.Failure("invalid_camp", validation);
        var slots = await db.CampSlots.Where(x => x.CampId == id).ToListAsync(ct); if (slots.Any(x => x.Status != CampSlotStatus.Available)) return CampResult<CampView>.Failure("camp_has_bookings", "Camp schedule cannot change after bookings exist.");
        try { camp.Update(input.Name, input.Location, input.Latitude, input.Longitude, input.StartsAtUtc, input.EndsAtUtc, input.Capacity); } catch (InvalidOperationException ex) { return CampResult<CampView>.Failure("invalid_camp_state", ex.Message); }
        db.CampSlots.RemoveRange(slots); foreach (var time in SlotTimes(input)) db.CampSlots.Add(new CampSlot(camp.Id, time)); await db.SaveChangesAsync(ct); return CampResult<CampView>.Success(await Map(camp, ct));
    }

    public async Task<CampResult<CampView>> TransitionAsync(Guid id, CampStatus status, CancellationToken ct)
    {
        var camp = await db.DonationCamps.SingleOrDefaultAsync(x => x.Id == id, ct); if (camp is null) return CampResult<CampView>.Failure("camp_not_found", "Donation camp was not found.");
        if (!Transitions.TryGetValue(camp.Status, out var allowed) || !allowed.Contains(status)) return CampResult<CampView>.Failure("invalid_transition", $"Cannot transition camp from {camp.Status} to {status}.");
        var now = timeProvider.GetUtcNow();
        if (status == CampStatus.Scheduled && camp.StartsAtUtc <= now) return CampResult<CampView>.Failure("invalid_transition", "The camp start time has passed, so donors could not book it. Edit the schedule before scheduling it.");
        if (status == CampStatus.InProgress && now < camp.StartsAtUtc - EarlyStartWindow) return CampResult<CampView>.Failure("invalid_transition", $"The camp can be started at most {EarlyStartWindow.TotalMinutes:0} minutes before its start time.");
        camp.TransitionTo(status); if (status is CampStatus.Closed or CampStatus.Cancelled) { var slots = await db.CampSlots.Where(x => x.CampId == id).ToListAsync(ct); foreach (var slot in slots) { if (status == CampStatus.Closed) slot.MarkNoShow(); else slot.CancelByCamp(); } }
        await db.SaveChangesAsync(ct); return CampResult<CampView>.Success(await Map(camp, ct));
    }

    public async Task<CampResult<CampSlotView>> BookAsync(Guid campId, Guid donorUserId, Guid? preferredSlotId, CancellationToken ct)
    {
        var camp = await db.DonationCamps.SingleOrDefaultAsync(x => x.Id == campId, ct); if (camp is null) return CampResult<CampSlotView>.Failure("camp_not_found", "Donation camp was not found.");
        if (camp.Status != CampStatus.Scheduled || camp.StartsAtUtc <= timeProvider.GetUtcNow()) return CampResult<CampSlotView>.Failure("camp_not_bookable", "Camp is not open for booking.");
        var donor = await db.Donors.SingleOrDefaultAsync(x => x.UserId == donorUserId && x.IsActive, ct); if (donor is null) return CampResult<CampSlotView>.Failure("donor_profile_required", "Active donor profile is required.");
        if (donor.EligibilityStatus != EligibilityStatus.Eligible) return CampResult<CampSlotView>.Failure("donor_not_eligible", "Only eligible donors can book camp slots.");
        if (await db.CampSlots.AnyAsync(x => x.CampId == campId && x.DonorId == donor.Id && x.Status != CampSlotStatus.Cancelled, ct)) return CampResult<CampSlotView>.Failure("already_booked", "Donor already has a booking for this camp.");
        var slots = db.CampSlots.Where(x => x.CampId == campId && x.Status == CampSlotStatus.Available); var slot = preferredSlotId is null ? await slots.OrderBy(x => x.SlotTimeUtc).FirstOrDefaultAsync(ct) : await slots.SingleOrDefaultAsync(x => x.Id == preferredSlotId, ct);
        if (slot is null) return CampResult<CampSlotView>.Failure("slot_unavailable", "No matching slot is available.");
        try { slot.Book(donor.Id); await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return CampResult<CampSlotView>.Failure("slot_unavailable", "Slot was booked concurrently; choose another slot."); }
        await notifications.QueueUserNotificationAsync(donorUserId, "camp-booking-confirmed", new Dictionary<string, string> { ["campId"] = camp.Id.ToString(), ["campName"] = camp.Name, ["slotTimeUtc"] = slot.SlotTimeUtc.ToString("O") }, $"camp-booked:{donor.Id:N}:{slot.Id:N}:{slot.UpdatedAtUtc.UtcTicks}", ct); // per booking, so a rebook after cancelling is confirmed too
        return CampResult<CampSlotView>.Success(Map(slot));
    }

    public async Task<CampResult<CampSlotView>> CancelBookingAsync(Guid campId, Guid slotId, Guid donorUserId, CancellationToken ct)
    {
        var donorId = await db.Donors.Where(x => x.UserId == donorUserId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct); var slot = await db.CampSlots.SingleOrDefaultAsync(x => x.Id == slotId && x.CampId == campId, ct);
        if (slot is null) return CampResult<CampSlotView>.Failure("slot_not_found", "Camp slot was not found.");
        if (slot.DonorId is null) return CampResult<CampSlotView>.Failure("invalid_slot_state", "This slot has no booking to cancel; it may already be cancelled.");
        if (donorId is null || slot.DonorId != donorId) return CampResult<CampSlotView>.Failure("not_booking_owner", "Booking belongs to another donor.");
        var camp = await db.DonationCamps.AsNoTracking().SingleAsync(x => x.Id == campId, ct);
        if (camp.Status != CampStatus.Scheduled || camp.StartsAtUtc <= timeProvider.GetUtcNow()) return CampResult<CampSlotView>.Failure("invalid_slot_state", "Bookings can only be cancelled before the camp starts.");
        try { slot.Cancel(); await db.SaveChangesAsync(ct); return CampResult<CampSlotView>.Success(Map(slot)); } catch (InvalidOperationException ex) { return CampResult<CampSlotView>.Failure("invalid_slot_state", ex.Message); }
    }

    public async Task<CampResult<CampSlotView>> CheckInAsync(Guid campId, Guid slotId, CancellationToken ct)
    {
        var slot = await db.CampSlots.SingleOrDefaultAsync(x => x.Id == slotId && x.CampId == campId, ct); if (slot is null) return CampResult<CampSlotView>.Failure("slot_not_found", "Camp slot was not found.");
        if (await db.DonationCamps.Where(x => x.Id == campId).Select(x => x.Status).SingleAsync(ct) != CampStatus.InProgress) return CampResult<CampSlotView>.Failure("invalid_slot_state", "Donors can only be checked in while the camp is in progress.");
        try { slot.CheckIn(); await db.SaveChangesAsync(ct); return CampResult<CampSlotView>.Success(Map(slot)); } catch (InvalidOperationException ex) { return CampResult<CampSlotView>.Failure("invalid_slot_state", ex.Message); }
    }

    public async Task<CampResult<IReadOnlyList<CampSlotView>>> SlotsAsync(Guid campId, CancellationToken ct)
    {
        if (!await db.DonationCamps.AnyAsync(x => x.Id == campId, ct)) return CampResult<IReadOnlyList<CampSlotView>>.Failure("camp_not_found", "Donation camp was not found.");
        var slots = await db.CampSlots.AsNoTracking().Where(x => x.CampId == campId).OrderBy(x => x.SlotTimeUtc).ToListAsync(ct); return CampResult<IReadOnlyList<CampSlotView>>.Success(slots.Select(Map).ToList());
    }

    public async Task<CampResult<AttendanceReport>> AttendanceAsync(Guid campId, CancellationToken ct)
    {
        var camp = await db.DonationCamps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == campId, ct); if (camp is null) return CampResult<AttendanceReport>.Failure("camp_not_found", "Donation camp was not found."); var slots = await db.CampSlots.AsNoTracking().Where(x => x.CampId == campId).ToListAsync(ct);
        var checkedIn = slots.Count(x => x.Status == CampSlotStatus.CheckedIn); var booked = slots.Count(x => x.Status is CampSlotStatus.Booked or CampSlotStatus.CheckedIn or CampSlotStatus.NoShow); return CampResult<AttendanceReport>.Success(new(campId, camp.Capacity, booked, checkedIn, slots.Count(x => x.Status == CampSlotStatus.NoShow), slots.Count(x => x.Status == CampSlotStatus.Cancelled && x.DonorId != null), booked == 0 ? 0 : Math.Round(checkedIn * 100d / booked, 2)));
    }

    public async Task<CampResult<IReadOnlyList<CampRosterEntry>>> RosterAsync(Guid campId, CancellationToken ct)
    {
        if (!await db.DonationCamps.AnyAsync(x => x.Id == campId, ct)) return CampResult<IReadOnlyList<CampRosterEntry>>.Failure("camp_not_found", "Donation camp was not found.");
        var roster = await (from slot in db.CampSlots.AsNoTracking() where slot.CampId == campId && slot.DonorId != null
                            join donor in db.Donors.AsNoTracking() on slot.DonorId equals donor.Id
                            join user in db.Users.AsNoTracking() on donor.UserId equals user.Id
                            orderby slot.SlotTimeUtc
                            select new CampRosterEntry(slot.Id, slot.SlotTimeUtc, slot.Status, donor.Id, user.Email, donor.BloodType)).ToListAsync(ct);
        return CampResult<IReadOnlyList<CampRosterEntry>>.Success(roster);
    }

    private string? Validate(CampInput x)
    {
        if (string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 250) return "Camp name is required and must be at most 250 characters.";
        if (string.IsNullOrWhiteSpace(x.Location) || x.Location.Length > 500) return "Location is required and must be at most 500 characters.";
        if (x.StartsAtUtc <= timeProvider.GetUtcNow()) return "Start time must be in the future.";
        if (x.StartsAtUtc > timeProvider.GetUtcNow().AddYears(2)) return "Start time must be within two years; check the year.";
        if (x.EndsAtUtc <= x.StartsAtUtc) return "End time must be after the start time.";
        if (x.Capacity is < 1 or > 1000) return "Capacity must be between 1 and 1000.";
        if (x.SlotMinutes is < 5 or > 240) return "Slot minutes must be between 5 and 240.";
        if (x.StartsAtUtc.AddMinutes((long)x.Capacity * x.SlotMinutes) > x.EndsAtUtc) return $"{x.Capacity} slots of {x.SlotMinutes} minutes need {(long)x.Capacity * x.SlotMinutes} minutes, which is longer than the camp. Extend the end time, or reduce capacity or slot length.";
        if (x.Latitude is < -90 or > 90 || x.Longitude is < -180 or > 180) return "Coordinates are outside valid ranges.";
        return null;
    }
    /// <summary>Npgsql only stores UTC offsets; a client sending local time (e.g. +05:30) must not fail with a database error.</summary>
    private static CampInput ToUtc(CampInput x) => x with { StartsAtUtc = x.StartsAtUtc.ToUniversalTime(), EndsAtUtc = x.EndsAtUtc.ToUniversalTime() };
    private static IEnumerable<DateTimeOffset> SlotTimes(CampInput x) { for (var i = 0; i < x.Capacity; i++) yield return x.StartsAtUtc.AddMinutes((long)i * x.SlotMinutes); }
    private async Task<CampView> Map(DonationCamp x, CancellationToken ct) => (await MapMany([x], ct))[0];
    /// <summary>Counts slots for all camps in one grouped query instead of one query per camp.</summary>
    private async Task<List<CampView>> MapMany(IReadOnlyList<DonationCamp> camps, CancellationToken ct)
    {
        var ids = camps.Select(c => c.Id).ToList();
        var counts = (await db.CampSlots.AsNoTracking().Where(s => ids.Contains(s.CampId)).GroupBy(s => new { s.CampId, s.Status }).Select(g => new { g.Key.CampId, g.Key.Status, Count = g.Count() }).ToListAsync(ct)).ToLookup(x => x.CampId);
        int Count(Guid campId, CampSlotStatus status) => counts[campId].Where(x => x.Status == status).Sum(x => x.Count);
        return camps.Select(x => new CampView(x.Id, x.OrganizerUserId, x.Name, x.Location, x.Latitude, x.Longitude, x.StartsAtUtc, x.EndsAtUtc, x.Capacity, x.Status, Count(x.Id, CampSlotStatus.Available), Count(x.Id, CampSlotStatus.Booked) + Count(x.Id, CampSlotStatus.CheckedIn) + Count(x.Id, CampSlotStatus.NoShow), x.CreatedAtUtc)).ToList();
    }
    private static CampSlotView Map(CampSlot x) => new(x.Id, x.CampId, x.DonorId, x.SlotTimeUtc, x.Status, x.Version);
}
