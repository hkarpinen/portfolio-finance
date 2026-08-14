using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollapseScheduleOwnerToCreatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The two columns were always written with the same value, but created_by was nullable
            // and user_id was not — so user_id is the one that is certainly populated. Fill from it
            // before the column is made required, or an older row takes the whole migration down.
            migrationBuilder.Sql(@"
UPDATE finance.charge_schedules
SET created_by = user_id
WHERE created_by IS NULL;");

            migrationBuilder.DropIndex(
                name: "ix_charge_schedules_user_id",
                schema: "finance",
                table: "charge_schedules");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by",
                schema: "finance",
                table: "charge_schedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "finance",
                table: "charge_schedules");

            migrationBuilder.CreateIndex(
                name: "ix_charge_schedules_created_by",
                schema: "finance",
                table: "charge_schedules",
                column: "created_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_charge_schedules_created_by",
                schema: "finance",
                table: "charge_schedules");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by",
                schema: "finance",
                table: "charge_schedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "finance",
                table: "charge_schedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_charge_schedules_user_id",
                schema: "finance",
                table: "charge_schedules",
                column: "user_id");
        }
    }
}
