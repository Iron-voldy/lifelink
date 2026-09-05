using System.Text.Json;
using LifeLink.Application.Workflows;
using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeLink.Infrastructure.Persistence;

public sealed class DemoDataOptions
{
    public bool Enabled { get; set; }
    /// <summary>Shared password for every seeded demo account.</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Seeds a coherent, realistic data set (every role, table and lifecycle state) for local demos.
/// Runs once: it skips when any account on <see cref="EmailDomain"/> already exists, and it refuses to run in Production.
/// </summary>
public sealed class DemoDataSeeder(IServiceScopeFactory scopes, IOptions<DemoDataOptions> options, IHostEnvironment environment, ILogger<DemoDataSeeder> logger) : IHostedService
{
    public const string EmailDomain = "demo.lifelink.local";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var value = options.Value;
        if (!value.Enabled) return;
        if (environment.IsProduction()) throw new InvalidOperationException("Demo data seeding must not be enabled in Production.");
        if (!IsStrong(value.Password)) throw new InvalidOperationException("DemoData:Password must be a strong 12+ character password when demo seeding is enabled.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LifeLinkDbContext>();
        if (await db.Users.AnyAsync(x => x.Email.EndsWith("@" + EmailDomain), cancellationToken))
        {
            logger.LogInformation("Demo data already present; skipping seeding");
            return;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var summary = await new DemoDataBuilder(db, value.Password, DateTimeOffset.UtcNow).BuildAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Seeded demo data: {Summary}", summary);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    private static bool IsStrong(string value) => value.Length >= 12 && value.Any(char.IsUpper) && value.Any(char.IsLower) && value.Any(char.IsDigit) && value.Any(x => !char.IsLetterOrDigit(x));
}

internal sealed class DemoDataBuilder(LifeLinkDbContext db, string password, DateTimeOffset now)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly DateOnly _today = DateOnly.FromDateTime(now.UtcDateTime);
    private readonly Random _random = new(2026);

    private sealed record Place(string City, string Address, decimal Lat, decimal Lon);
    private static readonly Place[] Places =
    [
        new("Colombo", "Regent Street, Colombo 08", 6.9175m, 79.8680m),
        new("Kandy", "William Gopallawa Mawatha, Kandy", 7.2865m, 80.6320m),
        new("Galle", "Karapitiya, Galle", 6.0680m, 80.2260m),
        new("Jaffna", "Hospital Road, Jaffna", 9.6650m, 80.0170m),
        new("Anuradhapura", "Harischandra Mawatha, Anuradhapura", 8.3420m, 80.4050m),
        new("Kurunegala", "Dambulla Road, Kurunegala", 7.4860m, 80.3620m),
        new("Ratnapura", "Hospital Road, Ratnapura", 6.6850m, 80.3990m),
        new("Badulla", "Passara Road, Badulla", 6.9890m, 81.0570m),
        new("Batticaloa", "Bar Road, Batticaloa", 7.7170m, 81.7000m),
        new("Matara", "Kamburugamuwa, Matara", 5.9480m, 80.5480m),
    ];

    private readonly List<User> _admins = [], _coordinators = [], _requesters = [], _donorUsers = [];
    private readonly List<Donor> _donors = [];
    private readonly List<Hospital> _hospitals = [];
    private readonly Dictionary<Guid, Guid> _staffHospital = [];
    private readonly List<BloodBankLocation> _locations = [];
    private readonly List<InventoryLot> _lots = [];
    private readonly Dictionary<string, int> _counts = [];

    public async Task<string> BuildAsync(CancellationToken ct)
    {
        CreateUsers();
        CreateHospitals();
        CreateDonors();
        CreateInventory();
        var camps = CreateCamps();
        await db.SaveChangesAsync(ct);
        CreateRequestsAndWorkflows();
        CreateCampNotifications(camps);
        CreateDeviceTokens();
        await db.SaveChangesAsync(ct);
        return string.Join(", ", _counts.Select(x => $"{x.Value} {x.Key}"));
    }

    private void Count(string table, int n = 1) => _counts[table] = _counts.GetValueOrDefault(table) + n;
    private T Add<T>(T entity) where T : class { db.Add(entity); Count(typeof(T).Name); return entity; }

    // ---- Users and roles -------------------------------------------------------------------------------------------

    private void CreateUsers()
    {
        // PBKDF2 is deliberately slow; one hash shared by every demo account keeps startup fast (demo-only data).
        var hash = new PasswordHasher<User>().HashPassword(null!, password);
        User NewUser(string prefix, int i, UserRole role)
        {
            var user = Add(new User($"{prefix}{i:00}@{DemoDataSeeder.EmailDomain}", string.Empty, role));
            user.SetPasswordHash(hash);
            return user;
        }
        for (var i = 1; i <= 10; i++) _admins.Add(NewUser("admin", i, UserRole.BloodBankAdmin));
        for (var i = 1; i <= 10; i++) _coordinators.Add(NewUser("coordinator", i, UserRole.CampCoordinator));
        for (var i = 1; i <= 12; i++) _requesters.Add(NewUser("hospital", i, UserRole.HospitalRequester));
        for (var i = 1; i <= 20; i++) _donorUsers.Add(NewUser("donor", i, UserRole.Donor));
    }

    // ---- Hospitals and staff -----------------------------------------------------------------------------------------

    private void CreateHospitals()
    {
        string[] names =
        [
            "National Hospital of Sri Lanka", "Teaching Hospital Kandy", "Teaching Hospital Karapitiya", "Teaching Hospital Jaffna",
            "Teaching Hospital Anuradhapura", "Teaching Hospital Kurunegala", "Teaching Hospital Ratnapura", "Provincial General Hospital Badulla",
            "Teaching Hospital Batticaloa", "District General Hospital Matara",
        ];
        for (var i = 0; i < names.Length; i++)
        {
            var place = Places[i];
            var hospital = Add(new Hospital(names[i], $"DEMO-HOSP-{i + 1:000}", place.Address, place.Lat, place.Lon));
            // Eight verified hospitals can raise requests; one awaits verification and one is suspended.
            hospital.SetVerification(i switch { 8 => VerificationStatus.Pending, 9 => VerificationStatus.Suspended, _ => VerificationStatus.Verified });
            _hospitals.Add(hospital);
        }
        string[] positions = ["Blood Bank Medical Officer", "Ward Sister", "Consultant Haematologist", "Registrar", "Nursing Officer"];
        for (var i = 0; i < _requesters.Count; i++)
        {
            // hospital01..10 map one-to-one; hospital11 and hospital12 are second staff members at the two largest hospitals.
            var hospital = _hospitals[i % _hospitals.Count];
            Add(new HospitalStaff(_requesters[i].Id, hospital.Id, positions[i % positions.Length]));
            _staffHospital[_requesters[i].Id] = hospital.Id;
        }
    }

    // ---- Donors, eligibility history and donation records ------------------------------------------------------------

    private void CreateDonors()
    {
        BloodType[] types =
        [
            BloodType.OPositive, BloodType.OPositive, BloodType.OPositive, BloodType.APositive, BloodType.APositive, BloodType.BPositive,
            BloodType.BPositive, BloodType.ONegative, BloodType.ONegative, BloodType.ANegative, BloodType.BNegative, BloodType.ABPositive,
            BloodType.ABNegative, BloodType.OPositive, BloodType.APositive, BloodType.BPositive, BloodType.OPositive, BloodType.ANegative,
            BloodType.ABPositive, BloodType.OPositive,
        ];
        for (var i = 0; i < _donorUsers.Count; i++)
        {
            var place = Places[i % Places.Length];
            // Donor 20 is over 60 (permanently ineligible); everyone else is 19-55.
            var dob = i == 19 ? new DateOnly(1961, 3, 14) : new DateOnly(1971 + i * 17 % 36, 1 + i % 12, 1 + i * 7 % 28);
            string[] flags = i switch { 15 => ["Low haemoglobin at last screening"], 16 => ["Recent tattoo (under 6 months)"], _ => [] };
            var donor = Add(new Donor(_donorUsers[i].Id, types[i], dob, $"{10 + i * 3}, {place.Address}", place.Lat + i * 0.001m, place.Lon - i * 0.001m, JsonSerializer.Serialize(flags)));
            _donors.Add(donor);

            // Donors 1-15 last donated more than 90 days ago; past camps later move some of them into the 90-day wait.
            if (i < 15)
            {
                var visits = 1 + i % 3;
                for (var v = 0; v < visits; v++)
                {
                    var date = _today.AddDays(-(120 + i * 5 + v * 150));
                    Add(new DonationRecord(donor.Id, date, 1, $"Regional Blood Centre {Places[(i + v) % Places.Length].City}", v == 0 ? null : "Routine walk-in donation"));
                    if (v == 0) donor.RecordDonation(date);
                }
            }

            var (status, reason) = i switch
            {
                < 15 => (EligibilityStatus.Eligible, "All deterministic eligibility checks passed."),
                < 17 => (EligibilityStatus.TemporarilyIneligible, "Medical review required: " + flags[0]),
                < 19 => (EligibilityStatus.PendingVerification, ""),
                _ => (EligibilityStatus.PermanentlyIneligible, "Donor is above the maximum donation age of 60."),
            };
            if (status != EligibilityStatus.PendingVerification)
            {
                Add(new DonorEligibilityHistory(donor.Id, EligibilityStatus.PendingVerification, status, reason, _admins[i % _admins.Count].Id));
                donor.ApplyEligibility(status);
            }
        }
        _donors[19].Deactivate();
    }

    // ---- Blood bank locations and inventory lots ---------------------------------------------------------------------

    private void CreateInventory()
    {
        for (var i = 0; i < Places.Length; i++)
        {
            var place = Places[i];
            var name = i == 0 ? "National Blood Centre, Narahenpita" : $"Regional Blood Centre {place.City}";
            _locations.Add(Add(new BloodBankLocation(name, place.Address, place.Lat + 0.01m, place.Lon + 0.01m)));
        }
        var allTypes = Enum.GetValues<BloodType>();
        string[] sources = ["Walk-in donations", "Mobile donation camp", "Transfer from National Blood Centre", "Hospital donor drive"];
        for (var l = 0; l < _locations.Count; l++)
        {
            // Five lots per centre, rotating through all blood types; O-negative (universal donor) is stocked everywhere.
            var types = new[] { BloodType.ONegative, allTypes[l % 8], allTypes[(l + 3) % 8], allTypes[(l + 5) % 8], BloodType.OPositive };
            for (var t = 0; t < types.Length; t++)
            {
                var n = l * types.Length + t;
                // Red cells keep ~42 days: spread expiry from already expired to fresh, with several near-expiry lots.
                var expiry = _today.AddDays(n % 9 == 0 ? -3 : 2 + n * 7 % 40);
                var lot = Add(new InventoryLot(_locations[l].Id, types[t], 8 + n * 5 % 23, expiry, sources[n % sources.Length]));
                if (expiry < _today) lot.MarkExpired();
                else if (n % 13 == 4) lot.Quarantine();
                _lots.Add(lot);
            }
        }
    }

    // ---- Donation camps and slots ------------------------------------------------------------------------------------

    private sealed record CampPlan(DonationCamp Camp, List<CampSlot> Slots);

    private List<CampPlan> CreateCamps()
    {
        (string Name, int DayOffset, CampStatus Status)[] specs =
        [
            ("University of Colombo Blood Drive", -40, CampStatus.Closed),
            ("Kandy City Centre Donation Day", -20, CampStatus.Closed),
            ("Galle Fort Community Camp", -5, CampStatus.Cancelled),
            ("Jaffna Youth Blood Camp", 0, CampStatus.InProgress),
            ("Anuradhapura Sacred City Drive", 3, CampStatus.Scheduled),
            ("Kurunegala Clock Tower Camp", 7, CampStatus.Scheduled),
            ("Ratnapura Gem Traders Drive", 10, CampStatus.Scheduled),
            ("Badulla Railway Station Camp", 14, CampStatus.Scheduled),
            ("Batticaloa Lagoon Side Camp", 21, CampStatus.Scheduled),
            ("Matara Beach Road Blood Drive", 30, CampStatus.Draft),
        ];
        var plans = new List<CampPlan>();
        const int capacity = 12, slotMinutes = 20;
        for (var c = 0; c < specs.Length; c++)
        {
            var place = Places[c];
            var starts = new DateTimeOffset(_today.AddDays(specs[c].DayOffset).ToDateTime(new TimeOnly(3, 30)), TimeSpan.Zero); // 09:00 Sri Lanka time
            var camp = Add(new DonationCamp(_coordinators[c].Id, specs[c].Name, $"Community Hall, {place.Address}", place.Lat - 0.005m, place.Lon + 0.005m, starts, starts.AddHours(6), capacity));
            camp.TransitionTo(specs[c].Status);
            var slots = Enumerable.Range(0, capacity).Select(i => Add(new CampSlot(camp.Id, starts.AddMinutes(i * slotMinutes)))).ToList();
            plans.Add(new(camp, slots));
        }

        // Past camps: attendees checked in (with a matching donation record) or did not show up.
        BookAndResolve(plans[0], [12, 13, 14], [5]);
        BookAndResolve(plans[1], [0, 3, 6], [8]);
        foreach (var slot in plans[2].Slots) slot.CancelByCamp();
        BookAndResolve(plans[3], [], []);
        plans[3].Slots[0].Book(_donors[1].Id);
        plans[3].Slots[1].Book(_donors[2].Id);
        // Upcoming camps are booked only by donors who are currently eligible.
        var eligible = _donors.Where(x => x.IsActive && x.EligibilityStatus == EligibilityStatus.Eligible).ToList();
        for (var c = 4; c < 9; c++)
            for (var b = 0; b < 3 + c % 3; b++) plans[c].Slots[b * 2].Book(eligible[(c * 3 + b) % eligible.Count].Id);
        return plans;
    }

    private void BookAndResolve(CampPlan plan, int[] checkedIn, int[] noShows)
    {
        var slot = 0;
        foreach (var d in checkedIn)
        {
            var s = plan.Slots[slot++];
            s.Book(_donors[d].Id);
            s.CheckIn();
            var donor = _donors[d];
            var date = DateOnly.FromDateTime(plan.Camp.StartsAtUtc.UtcDateTime);
            Add(new DonationRecord(donor.Id, date, 1, plan.Camp.Location, $"Donated at {plan.Camp.Name}"));
            donor.RecordDonation(date);
            // Same rule as DonorService.EvaluateEligibilityAsync: 90 days must pass before the next donation.
            if (donor.EligibilityStatus != EligibilityStatus.TemporarilyIneligible)
            {
                Add(new DonorEligibilityHistory(donor.Id, donor.EligibilityStatus, EligibilityStatus.TemporarilyIneligible, "A 90-day interval is required after the previous donation.", _admins[0].Id));
                donor.ApplyEligibility(EligibilityStatus.TemporarilyIneligible);
            }
        }
        foreach (var d in noShows) { var s = plan.Slots[slot++]; s.Book(_donors[d].Id); s.MarkNoShow(); }
    }

    // ---- Blood requests, agent workflows, approvals, reservations and dispatches -------------------------------------

    private sealed record RequestSpec(int Requester, BloodType Type, int Units, RequestUrgency Urgency, string Scenario, string? Notes);

    private void CreateRequestsAndWorkflows()
    {
        RequestSpec[] specs =
        [
            new(0, BloodType.OPositive, 4, RequestUrgency.Urgent, "submitted", "Road traffic accident, two casualties in theatre."),
            new(1, BloodType.APositive, 2, RequestUrgency.Routine, "submitted", "Elective hip replacement scheduled."),
            new(2, BloodType.BNegative, 3, RequestUrgency.Critical, "submitted", "Post-partum haemorrhage."),
            new(3, BloodType.ABPositive, 2, RequestUrgency.Routine, "review", "Thalassaemia transfusion, regular patient."),
            new(4, BloodType.ONegative, 3, RequestUrgency.Critical, "pending", "Neonatal exchange transfusion."),
            new(5, BloodType.ANegative, 2, RequestUrgency.Urgent, "pending", "Upper GI bleed."),
            new(6, BloodType.BPositive, 4, RequestUrgency.Urgent, "revised-pending", "Cardiac bypass surgery tomorrow morning."),
            new(7, BloodType.OPositive, 3, RequestUrgency.Urgent, "dispatched", "Dengue haemorrhagic fever ward."),
            new(10, BloodType.APositive, 5, RequestUrgency.Critical, "fulfilled-shortage", "Multiple trauma, massive transfusion protocol."),
            new(1, BloodType.BPositive, 2, RequestUrgency.Routine, "fulfilled", "Chronic kidney disease anaemia."),
            new(2, BloodType.ABNegative, 6, RequestUrgency.Critical, "escalated", "Rare type, liver transplant."),
            new(3, BloodType.OPositive, 2, RequestUrgency.Routine, "rejected", "Duplicate of an earlier request."),
            new(11, BloodType.ONegative, 2, RequestUrgency.Urgent, "fulfilled", "Emergency caesarean section."),
            new(4, BloodType.ABPositive, 3, RequestUrgency.Urgent, "fulfilled", "Oncology ward, chemotherapy support."),
            new(5, BloodType.ANegative, 1, RequestUrgency.Routine, "fulfilled", "Pre-operative cross-match."),
            new(6, BloodType.OPositive, 4, RequestUrgency.Critical, "revised-fulfilled", "Burns unit, extensive injuries."),
        ];
        for (var i = 0; i < specs.Length; i++) CreateRequest(specs[i], i);
    }

    private void CreateRequest(RequestSpec spec, int index)
    {
        var requester = _requesters[spec.Requester];
        var approver = _admins[index % _admins.Count];
        var request = Add(new BloodRequest(_staffHospital[requester.Id], requester.Id, spec.Type, spec.Units, spec.Urgency, spec.Notes, now.AddHours(spec.Urgency switch { RequestUrgency.Critical => 6, RequestUrgency.Urgent => 24, _ => 96 } + index)));
        switch (spec.Scenario)
        {
            case "submitted":
                break;
            case "review":
                Move(request, BloodRequestStatus.UnderReview, approver.Id, "Blood bank reviewing stock manually.");
                break;
            case "pending":
                RunWorkflow(request, 1, WorkflowStatus.PendingApproval);
                break;
            case "revised-pending":
                Revise(RunWorkflow(request, 1, WorkflowStatus.PendingApproval), approver, "Prefer lots from the nearest centre.");
                RunWorkflow(request, 2, WorkflowStatus.PendingApproval);
                break;
            case "dispatched" or "fulfilled" or "fulfilled-shortage":
                Approve(request, RunWorkflow(request, 1, WorkflowStatus.PendingApproval, spec.Scenario == "fulfilled-shortage"), approver, spec.Scenario != "dispatched");
                break;
            case "revised-fulfilled":
                Revise(RunWorkflow(request, 1, WorkflowStatus.PendingApproval), approver, "Recheck for fresher lots before dispatch.");
                Approve(request, RunWorkflow(request, 2, WorkflowStatus.PendingApproval), approver, true);
                break;
            case "escalated":
                RunWorkflow(request, 1, WorkflowStatus.EscalationRequired);
                break;
            case "rejected":
                var workflow = RunWorkflow(request, 1, WorkflowStatus.PendingApproval);
                Add(new AgentApproval(workflow.Id, 1, approver.Id, ApprovalDecision.Rejected, "Duplicate request; the original is already being fulfilled.", now));
                workflow.Finish(WorkflowStatus.Rejected, workflow.FinalOutcomeJson, now);
                Move(request, BloodRequestStatus.ClosedUnfulfilled, approver.Id, "Agent proposal rejected.");
                break;
        }
    }

    private void Move(BloodRequest request, BloodRequestStatus status, Guid actor, string reason)
    {
        Add(new RequestStatusHistory(request.Id, request.Status, status, actor, reason));
        request.TransitionTo(status);
    }

    private List<InventoryLot> CompatibleLots(BloodType recipient)
    {
        BloodType[] compatible = recipient switch
        {
            BloodType.ONegative => [BloodType.ONegative],
            BloodType.OPositive => [BloodType.ONegative, BloodType.OPositive],
            BloodType.ANegative => [BloodType.ONegative, BloodType.ANegative],
            BloodType.APositive => [BloodType.ONegative, BloodType.OPositive, BloodType.ANegative, BloodType.APositive],
            BloodType.BNegative => [BloodType.ONegative, BloodType.BNegative],
            BloodType.BPositive => [BloodType.ONegative, BloodType.OPositive, BloodType.BNegative, BloodType.BPositive],
            BloodType.ABNegative => [BloodType.ONegative, BloodType.ANegative, BloodType.BNegative, BloodType.ABNegative],
            _ => Enum.GetValues<BloodType>(),
        };
        return _lots.Where(x => compatible.Contains(x.BloodType) && x.Status == InventoryLotStatus.Available && x.UnitsAvailable > 0 && x.ExpiryDate >= _today)
            .OrderBy(x => x.ExpiryDate).ThenBy(x => x.BloodType == recipient ? 0 : 1).ToList();
    }

    /// <summary>Records a four-agent run exactly as the agent service would report it.</summary>
    private AgentWorkflowExecution RunWorkflow(BloodRequest request, int attempt, WorkflowStatus outcome, bool shortage = false)
    {
        var workflow = Add(new AgentWorkflowExecution(request.Id, attempt, $"Fulfil request {request.Id}: {(attempt == 1 ? "request submitted" : "revision requested")}", Guid.NewGuid().ToString("N")));
        workflow.Start(now.AddMinutes(-30 + attempt));
        Move(request, BloodRequestStatus.UnderReview, request.RequestedByUserId, $"Agent workflow attempt {attempt} started.");

        var lots = CompatibleLots(request.BloodType);
        var stockUnits = lots.Sum(x => x.UnitsAvailable);
        var escalate = outcome == WorkflowStatus.EscalationRequired;
        // A shortage scenario reserves part of the order and broadcasts to matching eligible donors for the rest.
        var reserve = escalate ? 0 : shortage ? Math.Max(1, request.QuantityUnits - 2) : Math.Min(request.QuantityUnits, stockUnits);
        var recipients = escalate || shortage
            ? _donors.Where(x => x.IsActive && x.EligibilityStatus == EligibilityStatus.Eligible && x.BloodType == request.BloodType).Select(x => x.UserId.ToString()).ToList()
            : [];
        var allocations = new List<object>();
        var remaining = reserve;
        foreach (var lot in lots.TakeWhile(_ => remaining > 0))
        {
            var take = Math.Min(remaining, lot.UnitsAvailable);
            allocations.Add(new { lot_id = lot.Id.ToString(), units = take });
            remaining -= take;
        }
        var plan = new Dictionary<string, object?>
        {
            ["objective"] = $"Source {request.QuantityUnits} unit(s) of {request.BloodType} ({request.Urgency})",
            ["steps"] = new[] { "analyse_candidates", "propose_inventory_reservation", "propose_donor_broadcast", "deterministic_safety_gate" },
            ["priority"] = request.Urgency.ToString(),
            ["model_used"] = "deterministic",
        };
        var analysis = new Dictionary<string, object?> { ["matched_donor_count"] = recipients.Count, ["compatible_stock_units"] = stockUnits, ["compatible_lots"] = allocations.Count, ["notes_classification"] = "untrusted-data" };
        var dispatch = new Dictionary<string, object?> { ["reserve_units"] = reserve, ["allocations"] = allocations, ["recipient_user_ids"] = recipients, ["shortage_units"] = request.QuantityUnits - reserve };
        var validation = new Dictionary<string, object?>
        {
            ["reservation_within_request"] = reserve <= request.QuantityUnits,
            ["requires_human_approval"] = !escalate,
            ["reasons"] = escalate ? new[] { "Compatible stock is below the critical rare-type order and no eligible donors match; manual escalation required." } : Array.Empty<string>(),
        };
        var outcomeText = escalate ? "EscalationRequired" : "PendingApproval";
        var steps = new List<AgentRunStep>
        {
            Step(1, "CoordinatorAgent", new() { ["request_id"] = request.Id.ToString() }, plan, []),
            Step(2, "DomainAnalysisAgent", new() { ["blood_type"] = request.BloodType.ToString(), ["notes"] = request.Notes }, analysis,
                [new Dictionary<string, object?> { ["name"] = "analyse_candidates", ["arguments"] = new { recipient_blood_type = request.BloodType.ToString() }, ["result"] = analysis }]),
            Step(3, "DispatchAgent", new() { ["required_units"] = request.QuantityUnits }, dispatch,
                [new Dictionary<string, object?> { ["name"] = "propose_inventory_reservation", ["arguments"] = new { required_units = request.QuantityUnits }, ["result"] = dispatch }]),
            Step(4, "ValidationSafetyAgent", new() { ["proposal"] = dispatch }, new(validation) { ["outcome"] = outcomeText },
                [new Dictionary<string, object?> { ["name"] = "deterministic_safety_gate", ["arguments"] = new { }, ["result"] = outcomeText }]),
        };
        var response = new AgentRunResponse("1.0", workflow.Id.ToString(), plan, steps, outcomeText, !escalate, validation, reserve, recipients);
        workflow.SetPlan(JsonSerializer.Serialize(plan, Json));
        foreach (var step in steps)
            Add(new AgentStep(workflow.Id, step.Sequence, step.AgentName, JsonSerializer.Serialize(step.Input, Json), JsonSerializer.Serialize(step.Output, Json),
                JsonSerializer.Serialize(step.ToolCalls, Json), AgentStepStatus.Completed, workflow.StartedAtUtc, workflow.StartedAtUtc!.Value.AddSeconds(step.Sequence * 2), null, null));
        workflow.Finish(outcome, JsonSerializer.Serialize(response, Json), now.AddMinutes(-29 + attempt));
        if (escalate) Move(request, BloodRequestStatus.Escalated, request.RequestedByUserId, "Agent workflow found no safe actionable match.");
        else Move(request, BloodRequestStatus.PendingApproval, request.RequestedByUserId, "Agent proposal passed deterministic validation and awaits human approval.");
        return workflow;
    }

    private static AgentRunStep Step(int sequence, string agent, Dictionary<string, object?> input, Dictionary<string, object?> output, List<IReadOnlyDictionary<string, object?>> tools)
        => new(sequence, agent, input, output, tools, "Completed", null, null);

    private void Revise(AgentWorkflowExecution workflow, User approver, string comments)
    {
        Add(new AgentApproval(workflow.Id, 1, approver.Id, ApprovalDecision.RevisionRequested, comments, now.AddMinutes(-20)));
        workflow.SetStatus(WorkflowStatus.Revising);
    }

    /// <summary>Mirrors WorkflowService.ApproveAsync: approve, reserve per lot, dispatch, broadcast, then optionally mark fulfilled.</summary>
    private void Approve(BloodRequest request, AgentWorkflowExecution workflow, User approver, bool fulfilled)
    {
        var response = JsonSerializer.Deserialize<AgentRunResponse>(workflow.FinalOutcomeJson!, Json)!;
        Add(new AgentApproval(workflow.Id, 1, approver.Id, ApprovalDecision.Approved, "Stock verified against the physical register.", now.AddMinutes(-15)));
        workflow.SetStatus(WorkflowStatus.Approved);
        Move(request, BloodRequestStatus.Approved, approver.Id, "Agent proposal approved.");

        var remaining = response.ProposedReservationUnits;
        var part = 0;
        foreach (var lot in CompatibleLots(request.BloodType))
        {
            if (remaining == 0) break;
            // Split multi-unit orders across lots, as FEFO allocation does when the oldest lot runs short.
            var take = Math.Min(Math.Min(remaining, lot.UnitsAvailable), Math.Max(1, (response.ProposedReservationUnits + 1) / 2));
            lot.Reserve(take);
            var reservation = Add(new InventoryReservation(request.Id, lot.Id, take, now.AddMinutes(45), $"workflow-{workflow.Id:N}:{part++}"));
            reservation.MarkDispatched();
            Add(new DispatchRecord(request.Id, reservation.Id, take, now.AddMinutes(-10), approver.Id));
            remaining -= take;
        }

        foreach (var recipient in response.ProposedRecipientUserIds.Select(Guid.Parse))
        {
            var payload = new Dictionary<string, string> { ["requestId"] = request.Id.ToString(), ["bloodType"] = request.BloodType.ToString(), ["urgency"] = request.Urgency.ToString(), ["message"] = $"Urgent: {request.BloodType} donors needed nearby." };
            var notification = Add(new Notification(recipient, workflow.Id, "urgent-blood-request", "Push", JsonSerializer.Serialize(payload), $"workflow-{workflow.Id:N}-broadcast:{recipient:N}"));
            notification.MarkSent(now.AddMinutes(-9));
        }
        Move(request, BloodRequestStatus.Dispatched, approver.Id, "Approved workflow actions dispatched.");
        workflow.Finish(WorkflowStatus.Completed, workflow.FinalOutcomeJson, now.AddMinutes(-9));
        Notify(request.RequestedByUserId, "request-status-changed", new() { ["requestId"] = request.Id.ToString(), ["status"] = "Dispatched", ["message"] = $"{request.QuantityUnits} unit(s) of {request.BloodType} dispatched." }, $"request-{request.Id:N}-dispatched");
        if (fulfilled)
        {
            Move(request, BloodRequestStatus.Fulfilled, request.RequestedByUserId, "Units received and transfused.");
            Notify(approver.Id, "request-fulfilled", new() { ["requestId"] = request.Id.ToString(), ["message"] = "Hospital confirmed receipt." }, $"request-{request.Id:N}-fulfilled");
        }
    }

    // ---- Notifications and devices -----------------------------------------------------------------------------------

    private void Notify(Guid userId, string type, Dictionary<string, string> data, string key, bool failed = false)
    {
        var notification = Add(new Notification(userId, null, type, "Push", JsonSerializer.Serialize(data), key));
        if (failed) notification.MarkFailed("Device token is no longer registered (UNREGISTERED).");
        else notification.MarkSent(now.AddMinutes(-_random.Next(5, 600)));
    }

    private void CreateCampNotifications(List<CampPlan> camps)
    {
        foreach (var plan in camps.Where(x => x.Camp.Status == CampStatus.Scheduled))
            foreach (var slot in plan.Slots.Where(x => x.DonorId is not null))
            {
                var donor = _donors.Single(x => x.Id == slot.DonorId);
                Notify(donor.UserId, "camp-reminder", new() { ["campId"] = plan.Camp.Id.ToString(), ["slotTime"] = slot.SlotTimeUtc.ToString("O"), ["message"] = $"Reminder: your slot at {plan.Camp.Name}." },
                    $"camp-{plan.Camp.Id:N}-reminder:{donor.UserId:N}", failed: slot.SlotTimeUtc.Minute == 40);
            }
        foreach (var donor in _donors.Take(3))
            Notify(donor.UserId, "camp-cancelled", new() { ["campId"] = camps[2].Camp.Id.ToString(), ["message"] = $"{camps[2].Camp.Name} was cancelled due to weather." }, $"camp-{camps[2].Camp.Id:N}-cancelled:{donor.UserId:N}");
    }

    private void CreateDeviceTokens()
    {
        for (var i = 0; i < 14; i++)
        {
            var token = Add(new DeviceToken(_donorUsers[i].Id, $"demo-fcm-token-donor{i + 1:00}-{Guid.NewGuid():N}", i % 3 == 0 ? "ios" : "android"));
            if (i == 13) token.Deactivate();
        }
        for (var i = 0; i < 4; i++) Add(new DeviceToken(_requesters[i].Id, $"demo-fcm-token-hospital{i + 1:00}-{Guid.NewGuid():N}", "android"));
    }
}
