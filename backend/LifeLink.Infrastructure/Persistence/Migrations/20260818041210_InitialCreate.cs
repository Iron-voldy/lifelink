using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeLink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lifelink");

            migrationBuilder.CreateTable(
                name: "blood_bank_locations",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blood_bank_locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hospitals",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VerificationStatus = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hospitals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_lots",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodType = table.Column<string>(type: "text", nullable: false),
                    UnitsReceived = table.Column<int>(type: "integer", nullable: false),
                    UnitsAvailable = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_lots", x => x.Id);
                    table.CheckConstraint("ck_inventory_lots_available", "\"UnitsAvailable\" >= 0 AND \"UnitsAvailable\" <= \"UnitsReceived\"");
                    table.CheckConstraint("ck_inventory_lots_received", "\"UnitsReceived\" > 0");
                    table.ForeignKey(
                        name: "FK_inventory_lots_blood_bank_locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "lifelink",
                        principalTable: "blood_bank_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "blood_requests",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodType = table.Column<string>(type: "text", nullable: false),
                    QuantityUnits = table.Column<int>(type: "integer", nullable: false),
                    Urgency = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequiredByUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blood_requests", x => x.Id);
                    table.CheckConstraint("ck_blood_requests_quantity", "\"QuantityUnits\" > 0");
                    table.ForeignKey(
                        name: "FK_blood_requests_hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalSchema: "lifelink",
                        principalTable: "hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_blood_requests_users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "device_tokens",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Platform = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_tokens_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "donation_camps",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donation_camps", x => x.Id);
                    table.CheckConstraint("ck_donation_camps_capacity", "\"Capacity\" > 0");
                    table.CheckConstraint("ck_donation_camps_time", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                    table.ForeignKey(
                        name: "FK_donation_camps_users_OrganizerUserId",
                        column: x => x.OrganizerUserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "donors",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    LastDonationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EligibilityStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MedicalFlagsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_donors_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hospital_staff",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HospitalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hospital_staff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hospital_staff_hospitals_HospitalId",
                        column: x => x.HospitalId,
                        principalSchema: "lifelink",
                        principalTable: "hospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hospital_staff_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "text", nullable: true),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_workflow_executions",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Objective = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalOutcomeJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_workflow_executions", x => x.Id);
                    table.CheckConstraint("ck_workflow_attempt", "\"AttemptNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_agent_workflow_executions_blood_requests_BloodRequestId",
                        column: x => x.BloodRequestId,
                        principalSchema: "lifelink",
                        principalTable: "blood_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReleaseReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.Id);
                    table.CheckConstraint("ck_inventory_reservations_units", "\"Units\" > 0");
                    table.ForeignKey(
                        name: "FK_inventory_reservations_blood_requests_BloodRequestId",
                        column: x => x.BloodRequestId,
                        principalSchema: "lifelink",
                        principalTable: "blood_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_inventory_lots_InventoryLotId",
                        column: x => x.InventoryLotId,
                        principalSchema: "lifelink",
                        principalTable: "inventory_lots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_status_history",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "text", nullable: false),
                    NewStatus = table.Column<string>(type: "text", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_status_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_request_status_history_blood_requests_BloodRequestId",
                        column: x => x.BloodRequestId,
                        principalSchema: "lifelink",
                        principalTable: "blood_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_request_status_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "camp_slots",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampId = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SlotTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camp_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_camp_slots_donation_camps_CampId",
                        column: x => x.CampId,
                        principalSchema: "lifelink",
                        principalTable: "donation_camps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_camp_slots_donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "lifelink",
                        principalTable: "donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "donor_eligibility_history",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "text", nullable: false),
                    NewStatus = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donor_eligibility_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_donor_eligibility_history_donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "lifelink",
                        principalTable: "donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_donor_eligibility_history_users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_approvals",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_approvals", x => x.Id);
                    table.CheckConstraint("ck_agent_approvals_version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_agent_approvals_agent_workflow_executions_WorkflowExecution~",
                        column: x => x.WorkflowExecutionId,
                        principalSchema: "lifelink",
                        principalTable: "agent_workflow_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_approvals_users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_steps",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: false),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: true),
                    ToolCallsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_steps", x => x.Id);
                    table.CheckConstraint("ck_agent_steps_sequence", "\"Sequence\" > 0");
                    table.ForeignKey(
                        name: "FK_agent_steps_agent_workflow_executions_WorkflowExecutionId",
                        column: x => x.WorkflowExecutionId,
                        principalSchema: "lifelink",
                        principalTable: "agent_workflow_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_agent_workflow_executions_WorkflowExecutionId",
                        column: x => x.WorkflowExecutionId,
                        principalSchema: "lifelink",
                        principalTable: "agent_workflow_executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_records",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitsDispatched = table.Column<int>(type: "integer", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_records", x => x.Id);
                    table.CheckConstraint("ck_dispatch_records_units", "\"UnitsDispatched\" > 0");
                    table.ForeignKey(
                        name: "FK_dispatch_records_blood_requests_BloodRequestId",
                        column: x => x.BloodRequestId,
                        principalSchema: "lifelink",
                        principalTable: "blood_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_records_inventory_reservations_InventoryReservatio~",
                        column: x => x.InventoryReservationId,
                        principalSchema: "lifelink",
                        principalTable: "inventory_reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispatch_records_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "lifelink",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_approvals_ApproverUserId",
                schema: "lifelink",
                table: "agent_approvals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_approvals_WorkflowExecutionId_Version",
                schema: "lifelink",
                table: "agent_approvals",
                columns: new[] { "WorkflowExecutionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_steps_WorkflowExecutionId_Sequence",
                schema: "lifelink",
                table: "agent_steps",
                columns: new[] { "WorkflowExecutionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_workflow_executions_BloodRequestId_AttemptNumber",
                schema: "lifelink",
                table: "agent_workflow_executions",
                columns: new[] { "BloodRequestId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_workflow_executions_CorrelationId",
                schema: "lifelink",
                table: "agent_workflow_executions",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_blood_requests_HospitalId_CreatedAtUtc",
                schema: "lifelink",
                table: "blood_requests",
                columns: new[] { "HospitalId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_blood_requests_RequestedByUserId",
                schema: "lifelink",
                table: "blood_requests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_blood_requests_Status_Urgency",
                schema: "lifelink",
                table: "blood_requests",
                columns: new[] { "Status", "Urgency" });

            migrationBuilder.CreateIndex(
                name: "IX_camp_slots_CampId_DonorId",
                schema: "lifelink",
                table: "camp_slots",
                columns: new[] { "CampId", "DonorId" },
                unique: true,
                filter: "\"DonorId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_camp_slots_CampId_SlotTimeUtc",
                schema: "lifelink",
                table: "camp_slots",
                columns: new[] { "CampId", "SlotTimeUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_camp_slots_DonorId",
                schema: "lifelink",
                table: "camp_slots",
                column: "DonorId");

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_Token",
                schema: "lifelink",
                table: "device_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_UserId",
                schema: "lifelink",
                table: "device_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_records_ApprovedByUserId",
                schema: "lifelink",
                table: "dispatch_records",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_records_BloodRequestId",
                schema: "lifelink",
                table: "dispatch_records",
                column: "BloodRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_records_InventoryReservationId",
                schema: "lifelink",
                table: "dispatch_records",
                column: "InventoryReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_donation_camps_OrganizerUserId",
                schema: "lifelink",
                table: "donation_camps",
                column: "OrganizerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_donation_camps_Status_StartsAtUtc",
                schema: "lifelink",
                table: "donation_camps",
                columns: new[] { "Status", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_donor_eligibility_history_ChangedByUserId",
                schema: "lifelink",
                table: "donor_eligibility_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_donor_eligibility_history_DonorId_CreatedAtUtc",
                schema: "lifelink",
                table: "donor_eligibility_history",
                columns: new[] { "DonorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_donors_BloodType_EligibilityStatus",
                schema: "lifelink",
                table: "donors",
                columns: new[] { "BloodType", "EligibilityStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_donors_UserId",
                schema: "lifelink",
                table: "donors",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hospital_staff_HospitalId",
                schema: "lifelink",
                table: "hospital_staff",
                column: "HospitalId");

            migrationBuilder.CreateIndex(
                name: "IX_hospital_staff_UserId",
                schema: "lifelink",
                table: "hospital_staff",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_hospitals_RegistrationNumber",
                schema: "lifelink",
                table: "hospitals",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_lots_BloodType_ExpiryDate_Status",
                schema: "lifelink",
                table: "inventory_lots",
                columns: new[] { "BloodType", "ExpiryDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_lots_LocationId",
                schema: "lifelink",
                table: "inventory_lots",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_BloodRequestId_Status",
                schema: "lifelink",
                table: "inventory_reservations",
                columns: new[] { "BloodRequestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_IdempotencyKey",
                schema: "lifelink",
                table: "inventory_reservations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_InventoryLotId",
                schema: "lifelink",
                table: "inventory_reservations",
                column: "InventoryLotId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_IdempotencyKey",
                schema: "lifelink",
                table: "notifications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_RecipientUserId",
                schema: "lifelink",
                table: "notifications",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_Status_CreatedAtUtc",
                schema: "lifelink",
                table: "notifications",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_WorkflowExecutionId",
                schema: "lifelink",
                table: "notifications",
                column: "WorkflowExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                schema: "lifelink",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId_ExpiresAtUtc",
                schema: "lifelink",
                table: "refresh_tokens",
                columns: new[] { "UserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_request_status_history_BloodRequestId_CreatedAtUtc",
                schema: "lifelink",
                table: "request_status_history",
                columns: new[] { "BloodRequestId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_request_status_history_ChangedByUserId",
                schema: "lifelink",
                table: "request_status_history",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "lifelink",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_approvals",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "agent_steps",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "camp_slots",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "device_tokens",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "dispatch_records",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "donor_eligibility_history",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "hospital_staff",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "request_status_history",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "donation_camps",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "inventory_reservations",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "donors",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "agent_workflow_executions",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "inventory_lots",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "blood_requests",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "blood_bank_locations",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "hospitals",
                schema: "lifelink");

            migrationBuilder.DropTable(
                name: "users",
                schema: "lifelink");
        }
    }
}
