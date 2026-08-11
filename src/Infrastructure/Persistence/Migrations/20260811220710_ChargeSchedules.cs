using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChargeSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "occurrence_date",
                schema: "finance",
                table: "charges",
                type: "timestamp with time zone",
                nullable: false,
                // Backfilled immediately below. The generated default would date every existing
                // charge to year 1, which is not a period anything reports in.
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "schedule_id",
                schema: "finance",
                table: "charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "charge_schedules",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funding_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anchor_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charge_schedules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_charges_schedule_id_occurrence_date",
                schema: "finance",
                table: "charges",
                columns: new[] { "schedule_id", "occurrence_date" },
                unique: true,
                filter: "schedule_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_charge_schedules_group_id",
                schema: "finance",
                table: "charge_schedules",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_charge_schedules_user_id",
                schema: "finance",
                table: "charge_schedules",
                column: "user_id");

            // A charge already on the books belongs to the day it was due.
            migrationBuilder.Sql(@"
UPDATE finance.charges SET occurrence_date = date_trunc('day', due_date);");

            // Every recurring charge becomes a schedule holding its cadence, and the charge itself
            // becomes the first occurrence of that schedule. Existing ids keep pointing at a real
            // charge — nothing the frontend, household or notifications holds goes stale.
            migrationBuilder.Sql(@"
INSERT INTO finance.charge_schedules
    (id, user_id, group_id, created_by, payer_user_id, funding_source, title, description,
     amount, currency, category, frequency, anchor_date, end_date, created_at, updated_at, is_active)
SELECT gen_random_uuid(), c.user_id, c.group_id, c.created_by, c.payer_user_id, c.funding_source,
       c.title, c.description, c.amount, c.currency, c.category,
       c.recurrence_frequency, COALESCE(c.recurrence_start_date, c.due_date), c.recurrence_end_date,
       c.created_at, c.updated_at, c.is_active
FROM finance.charges c
WHERE c.recurrence_frequency IS NOT NULL;

UPDATE finance.charges c
SET schedule_id = s.id
FROM finance.charge_schedules s
WHERE c.recurrence_frequency IS NOT NULL
  AND s.title = c.title
  AND s.created_at = c.created_at
  AND s.user_id = c.user_id
  AND c.schedule_id IS NULL;");
        }



        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charge_schedules",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "ix_charges_schedule_id_occurrence_date",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "occurrence_date",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "schedule_id",
                schema: "finance",
                table: "charges");
        }
    }
}
