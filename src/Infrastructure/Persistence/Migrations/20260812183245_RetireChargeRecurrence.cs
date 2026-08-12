using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireChargeRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A charge lifted into a schedule kept its own copy of the cadence, so it was in both
            // models at once: read paths derived a MOVING occurrence from it while charges
            // generated from the schedule carried a frozen one, and the same list mixed the two.
            //
            // The cadence lives on the schedule. Clearing it here is the second half of the
            // handover the ChargeSchedules migration started.
            migrationBuilder.Sql(@"
UPDATE finance.charges
SET recurrence_frequency = NULL,
    recurrence_start_date = NULL,
    recurrence_end_date = NULL
WHERE schedule_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
