using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeLink.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDonorLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "lifelink",
                table: "donors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "donation_records",
                schema: "lifelink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DonorId = table.Column<Guid>(type: "uuid", nullable: false),
                    DonationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Units = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donation_records", x => x.Id);
                    table.CheckConstraint("ck_donation_records_units", "\"Units\" > 0");
                    table.ForeignKey(
                        name: "FK_donation_records_donors_DonorId",
                        column: x => x.DonorId,
                        principalSchema: "lifelink",
                        principalTable: "donors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_donation_records_DonorId_DonationDate",
                schema: "lifelink",
                table: "donation_records",
                columns: new[] { "DonorId", "DonationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "donation_records",
                schema: "lifelink");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "lifelink",
                table: "donors");
        }
    }
}
