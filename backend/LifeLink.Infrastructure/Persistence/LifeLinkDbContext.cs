using LifeLink.Domain.Common;
using LifeLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Infrastructure.Persistence;

public sealed class LifeLinkDbContext(DbContextOptions<LifeLinkDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<DonorEligibilityHistory> DonorEligibilityHistory => Set<DonorEligibilityHistory>();
    public DbSet<DonationRecord> DonationRecords => Set<DonationRecord>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<HospitalStaff> HospitalStaff => Set<HospitalStaff>();
    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();
    public DbSet<RequestStatusHistory> RequestStatusHistory => Set<RequestStatusHistory>();
    public DbSet<BloodBankLocation> BloodBankLocations => Set<BloodBankLocation>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<InventoryReservation> InventoryReservations => Set<InventoryReservation>();
    public DbSet<DispatchRecord> DispatchRecords => Set<DispatchRecord>();
    public DbSet<DonationCamp> DonationCamps => Set<DonationCamp>();
    public DbSet<CampSlot> CampSlots => Set<CampSlot>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AgentWorkflowExecution> AgentWorkflowExecutions => Set<AgentWorkflowExecution>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<AgentApproval> AgentApprovals => Set<AgentApproval>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("lifelink");
        ConfigureIdentity(modelBuilder);
        ConfigureDonorsAndRequests(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureCampsAndNotifications(modelBuilder);
        ConfigureWorkflows(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Entity>().Where(x => x.State == EntityState.Modified))
        {
            entry.Property(nameof(Entity.UpdatedAtUtc)).CurrentValue = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    private static void ConfigureIdentity(ModelBuilder b)
    {
        b.Entity<User>(e => { e.ToTable("users"); e.HasIndex(x => x.Email).IsUnique(); e.Property(x => x.Email).HasMaxLength(320); e.Property(x => x.PasswordHash).HasMaxLength(500); e.Property(x => x.Role).HasConversion<string>().HasMaxLength(40); });
        b.Entity<RefreshToken>(e => { e.ToTable("refresh_tokens"); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasIndex(x => new { x.UserId, x.ExpiresAtUtc }); e.Property(x => x.TokenHash).HasMaxLength(128); e.Property(x => x.DeviceName).HasMaxLength(200); e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<DeviceToken>(e => { e.ToTable("device_tokens"); e.HasIndex(x => x.Token).IsUnique(); e.Property(x => x.Token).HasMaxLength(500); e.Property(x => x.Platform).HasMaxLength(30); e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
    }

    private static void ConfigureDonorsAndRequests(ModelBuilder b)
    {
        b.Entity<Donor>(e => { e.ToTable("donors"); e.HasIndex(x => x.UserId).IsUnique(); e.HasIndex(x => new { x.BloodType, x.EligibilityStatus }); e.Property(x => x.BloodType).HasConversion<string>().HasMaxLength(20); e.Property(x => x.EligibilityStatus).HasConversion<string>().HasMaxLength(40); e.Property(x => x.MedicalFlagsJson).HasColumnType("jsonb"); e.Property(x => x.Address).HasMaxLength(500); e.Property(x => x.Latitude).HasPrecision(9, 6); e.Property(x => x.Longitude).HasPrecision(9, 6); e.HasOne<User>().WithOne(x => x.DonorProfile).HasForeignKey<Donor>(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<DonorEligibilityHistory>(e => { e.ToTable("donor_eligibility_history"); e.HasIndex(x => new { x.DonorId, x.CreatedAtUtc }); e.Property(x => x.PreviousStatus).HasConversion<string>(); e.Property(x => x.NewStatus).HasConversion<string>(); e.Property(x => x.Reason).HasMaxLength(1000); e.HasOne<Donor>().WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict); e.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<DonationRecord>(e => { e.ToTable("donation_records", t => t.HasCheckConstraint("ck_donation_records_units", "\"Units\" > 0")); e.HasIndex(x => new { x.DonorId, x.DonationDate }); e.Property(x => x.Location).HasMaxLength(500); e.Property(x => x.Notes).HasMaxLength(1000); e.HasOne<Donor>().WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Hospital>(e => { e.ToTable("hospitals"); e.HasIndex(x => x.RegistrationNumber).IsUnique(); e.Property(x => x.Name).HasMaxLength(250); e.Property(x => x.RegistrationNumber).HasMaxLength(100); e.Property(x => x.VerificationStatus).HasConversion<string>(); e.Property(x => x.Address).HasMaxLength(500); e.Property(x => x.Latitude).HasPrecision(9, 6); e.Property(x => x.Longitude).HasPrecision(9, 6); });
        b.Entity<HospitalStaff>(e => { e.ToTable("hospital_staff"); e.HasIndex(x => x.UserId).IsUnique(); e.Property(x => x.Position).HasMaxLength(150); e.HasOne<User>().WithOne(x => x.HospitalStaffProfile).HasForeignKey<HospitalStaff>(x => x.UserId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Hospital>().WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<BloodRequest>(e => { e.ToTable("blood_requests", t => t.HasCheckConstraint("ck_blood_requests_quantity", "\"QuantityUnits\" > 0")); e.HasIndex(x => new { x.Status, x.Urgency }); e.HasIndex(x => new { x.HospitalId, x.CreatedAtUtc }); e.Property(x => x.BloodType).HasConversion<string>(); e.Property(x => x.Urgency).HasConversion<string>(); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.Notes).HasMaxLength(2000); e.HasOne<Hospital>().WithMany().HasForeignKey(x => x.HospitalId).OnDelete(DeleteBehavior.Restrict); e.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<RequestStatusHistory>(e => { e.ToTable("request_status_history"); e.HasIndex(x => new { x.BloodRequestId, x.CreatedAtUtc }); e.Property(x => x.PreviousStatus).HasConversion<string>(); e.Property(x => x.NewStatus).HasConversion<string>(); e.Property(x => x.Reason).HasMaxLength(1000); e.HasOne<BloodRequest>().WithMany().HasForeignKey(x => x.BloodRequestId).OnDelete(DeleteBehavior.Restrict); e.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureInventory(ModelBuilder b)
    {
        b.Entity<BloodBankLocation>(e => { e.ToTable("blood_bank_locations"); e.Property(x => x.Name).HasMaxLength(250); e.Property(x => x.Address).HasMaxLength(500); e.Property(x => x.Latitude).HasPrecision(9, 6); e.Property(x => x.Longitude).HasPrecision(9, 6); });
        b.Entity<InventoryLot>(e => { e.ToTable("inventory_lots", t => { t.HasCheckConstraint("ck_inventory_lots_received", "\"UnitsReceived\" > 0"); t.HasCheckConstraint("ck_inventory_lots_available", "\"UnitsAvailable\" >= 0 AND \"UnitsAvailable\" <= \"UnitsReceived\""); }); e.HasIndex(x => new { x.BloodType, x.ExpiryDate, x.Status }); e.Property(x => x.BloodType).HasConversion<string>(); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.Source).HasMaxLength(200); e.Property(x => x.Version).IsRowVersion(); e.HasOne<BloodBankLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<InventoryReservation>(e => { e.ToTable("inventory_reservations", t => t.HasCheckConstraint("ck_inventory_reservations_units", "\"Units\" > 0")); e.HasIndex(x => x.IdempotencyKey).IsUnique(); e.HasIndex(x => new { x.BloodRequestId, x.Status }); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.IdempotencyKey).HasMaxLength(100); e.Property(x => x.ReleaseReason).HasMaxLength(500); e.HasOne<BloodRequest>().WithMany().HasForeignKey(x => x.BloodRequestId).OnDelete(DeleteBehavior.Restrict); e.HasOne<InventoryLot>().WithMany().HasForeignKey(x => x.InventoryLotId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<DispatchRecord>(e => { e.ToTable("dispatch_records", t => t.HasCheckConstraint("ck_dispatch_records_units", "\"UnitsDispatched\" > 0")); e.HasIndex(x => x.InventoryReservationId).IsUnique(); e.HasOne<BloodRequest>().WithMany().HasForeignKey(x => x.BloodRequestId).OnDelete(DeleteBehavior.Restrict); e.HasOne<InventoryReservation>().WithMany().HasForeignKey(x => x.InventoryReservationId).OnDelete(DeleteBehavior.Restrict); e.HasOne<User>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureCampsAndNotifications(ModelBuilder b)
    {
        b.Entity<DonationCamp>(e => { e.ToTable("donation_camps", t => { t.HasCheckConstraint("ck_donation_camps_capacity", "\"Capacity\" > 0"); t.HasCheckConstraint("ck_donation_camps_time", "\"EndsAtUtc\" > \"StartsAtUtc\""); }); e.HasIndex(x => new { x.Status, x.StartsAtUtc }); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.Name).HasMaxLength(250); e.Property(x => x.Location).HasMaxLength(500); e.Property(x => x.Latitude).HasPrecision(9, 6); e.Property(x => x.Longitude).HasPrecision(9, 6); e.HasOne<User>().WithMany().HasForeignKey(x => x.OrganizerUserId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<CampSlot>(e => { e.ToTable("camp_slots"); e.HasIndex(x => new { x.CampId, x.SlotTimeUtc }).IsUnique(); e.HasIndex(x => new { x.CampId, x.DonorId }).IsUnique().HasFilter("\"DonorId\" IS NOT NULL"); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.Version).IsRowVersion(); e.HasOne<DonationCamp>().WithMany().HasForeignKey(x => x.CampId).OnDelete(DeleteBehavior.Restrict); e.HasOne<Donor>().WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<Notification>(e => { e.ToTable("notifications"); e.HasIndex(x => x.IdempotencyKey).IsUnique(); e.HasIndex(x => new { x.Status, x.CreatedAtUtc }); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.Type).HasMaxLength(100); e.Property(x => x.Channel).HasMaxLength(30); e.Property(x => x.PayloadJson).HasColumnType("jsonb"); e.Property(x => x.IdempotencyKey).HasMaxLength(100); e.Property(x => x.LastError).HasMaxLength(2000); e.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict); e.HasOne<AgentWorkflowExecution>().WithMany().HasForeignKey(x => x.WorkflowExecutionId).OnDelete(DeleteBehavior.Restrict); });
    }

    private static void ConfigureWorkflows(ModelBuilder b)
    {
        b.Entity<AgentWorkflowExecution>(e => { e.ToTable("agent_workflow_executions", t => t.HasCheckConstraint("ck_workflow_attempt", "\"AttemptNumber\" > 0")); e.HasIndex(x => new { x.BloodRequestId, x.AttemptNumber }).IsUnique(); e.HasIndex(x => x.CorrelationId).IsUnique(); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.Objective).HasMaxLength(1000); e.Property(x => x.PlanJson).HasColumnType("jsonb"); e.Property(x => x.FinalOutcomeJson).HasColumnType("jsonb"); e.Property(x => x.CorrelationId).HasMaxLength(100); e.HasOne<BloodRequest>().WithMany().HasForeignKey(x => x.BloodRequestId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<AgentStep>(e => { e.ToTable("agent_steps", t => t.HasCheckConstraint("ck_agent_steps_sequence", "\"Sequence\" > 0")); e.HasIndex(x => new { x.WorkflowExecutionId, x.Sequence }).IsUnique(); e.Property(x => x.Status).HasConversion<string>(); e.Property(x => x.AgentName).HasMaxLength(100); e.Property(x => x.InputJson).HasColumnType("jsonb"); e.Property(x => x.OutputJson).HasColumnType("jsonb"); e.Property(x => x.ToolCallsJson).HasColumnType("jsonb"); e.Property(x => x.ErrorCode).HasMaxLength(100); e.Property(x => x.ErrorMessage).HasMaxLength(2000); e.HasOne<AgentWorkflowExecution>().WithMany().HasForeignKey(x => x.WorkflowExecutionId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<AgentApproval>(e => { e.ToTable("agent_approvals", t => t.HasCheckConstraint("ck_agent_approvals_version", "\"Version\" > 0")); e.HasIndex(x => new { x.WorkflowExecutionId, x.Version }).IsUnique(); e.Property(x => x.Decision).HasConversion<string>(); e.Property(x => x.Comments).HasMaxLength(2000); e.HasOne<AgentWorkflowExecution>().WithMany().HasForeignKey(x => x.WorkflowExecutionId).OnDelete(DeleteBehavior.Restrict); e.HasOne<User>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict); });
    }
}
