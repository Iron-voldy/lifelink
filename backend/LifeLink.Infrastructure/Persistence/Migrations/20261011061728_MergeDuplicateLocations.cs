using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeLink.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Merges blood-bank locations that share a name (ignoring case and spaces) into the oldest one, moving their
    /// stock lots across so no units are lost, then enforces unique names so the duplicates cannot come back.
    /// Tables are schema-qualified because raw SQL does not get the model's default "lifelink" schema.
    /// </summary>
    public partial class MergeDuplicateLocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", first_value("Id") OVER (PARTITION BY lower(btrim("Name")) ORDER BY "CreatedAtUtc", "Id") AS keep_id
                    FROM lifelink.blood_bank_locations)
                UPDATE lifelink.inventory_lots AS lot SET "LocationId" = ranked.keep_id
                FROM ranked WHERE lot."LocationId" = ranked."Id" AND ranked."Id" <> ranked.keep_id;
                """);
            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT "Id", first_value("Id") OVER (PARTITION BY lower(btrim("Name")) ORDER BY "CreatedAtUtc", "Id") AS keep_id
                    FROM lifelink.blood_bank_locations)
                DELETE FROM lifelink.blood_bank_locations AS loc USING ranked
                WHERE loc."Id" = ranked."Id" AND ranked."Id" <> ranked.keep_id;
                """);
            migrationBuilder.Sql("""CREATE UNIQUE INDEX "ux_blood_bank_locations_name" ON lifelink.blood_bank_locations (lower(btrim("Name")));""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Merged duplicates are not recreated; only the uniqueness rule is lifted.
            migrationBuilder.Sql("""DROP INDEX IF EXISTS lifelink."ux_blood_bank_locations_name";""");
        }
    }
}
