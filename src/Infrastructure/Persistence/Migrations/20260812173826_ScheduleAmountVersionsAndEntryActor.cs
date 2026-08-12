using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleAmountVersionsAndEntryActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "charge_schedule_amounts",
                schema: "finance",
                columns: table => new
                {
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charge_schedule_amounts", x => new { x.schedule_id, x.effective_from });
                    table.ForeignKey(
                        name: "fk_charge_schedule_amounts_charge_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalSchema: "finance",
                        principalTable: "charge_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Every schedule's existing amount becomes its first version, effective from its
            // anchor. Dropping the column first would leave AmountOn with nothing to answer with.
            migrationBuilder.Sql(@"
INSERT INTO finance.charge_schedule_amounts (schedule_id, effective_from, amount)
SELECT id, date_trunc('day', anchor_date), amount
FROM finance.charge_schedules;");

            migrationBuilder.DropColumn(
                name: "amount",
                schema: "finance",
                table: "charge_schedules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charge_schedule_amounts",
                schema: "finance");

            migrationBuilder.AddColumn<decimal>(
                name: "amount",
                schema: "finance",
                table: "charge_schedules",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
