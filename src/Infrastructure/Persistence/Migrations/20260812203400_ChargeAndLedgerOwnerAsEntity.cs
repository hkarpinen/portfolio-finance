using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChargeAndLedgerOwnerAsEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A charge belongs to one accounting entity. That used to be three columns saying it
            // between them: group_id as the discriminator, user_id meaning the OWNER on a personal
            // row and the CREATOR on a shared one, and created_by repeating the latter. So the new
            // columns are filled from the old ones before any of them go.
            //
            // owner_kind matches EntityKind: 0 = Household, 1 = Person.
            migrationBuilder.AddColumn<int>(
                name: "owner_kind", schema: "finance", table: "charges",
                type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id_new", schema: "finance", table: "charges",
                type: "uuid", nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "entered_by", schema: "finance", table: "charges",
                type: "uuid", nullable: true);

            migrationBuilder.Sql(@"
UPDATE finance.charges
SET owner_kind   = CASE WHEN group_id IS NOT NULL THEN 0 ELSE 1 END,
    owner_id_new = COALESCE(group_id, user_id),
    -- created_by was only ever set on a shared row; on a personal one the owner entered it.
    entered_by   = COALESCE(created_by, user_id);");

            migrationBuilder.DropIndex(name: "ix_charges_group_id_is_active", schema: "finance", table: "charges");
            migrationBuilder.DropIndex(name: "ix_charges_user_id_is_active", schema: "finance", table: "charges");

            migrationBuilder.DropColumn(name: "created_by", schema: "finance", table: "charges");
            migrationBuilder.DropColumn(name: "group_id", schema: "finance", table: "charges");
            migrationBuilder.DropColumn(name: "user_id", schema: "finance", table: "charges");

            migrationBuilder.RenameColumn(
                name: "owner_id_new", schema: "finance", table: "charges", newName: "owner_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "owner_id", schema: "finance", table: "charges",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(
                name: "entered_by", schema: "finance", table: "charges",
                type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

            migrationBuilder.Sql(
                "CREATE INDEX ix_charges_owner_kind_owner_id_is_active " +
                "ON finance.charges (owner_kind, owner_id, is_active);");

            // Same collapse on the agreement. created_by stays — on a schedule it always meant
            // whoever opened it, and the earlier migration already backfilled it.
            migrationBuilder.AddColumn<int>(
                name: "owner_kind", schema: "finance", table: "charge_schedules",
                type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_id", schema: "finance", table: "charge_schedules",
                type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
UPDATE finance.charge_schedules
SET owner_kind = CASE WHEN group_id IS NOT NULL THEN 0 ELSE 1 END,
    owner_id   = COALESCE(group_id, created_by);");

            migrationBuilder.DropIndex(name: "ix_charge_schedules_group_id", schema: "finance", table: "charge_schedules");
            migrationBuilder.DropColumn(name: "group_id", schema: "finance", table: "charge_schedules");

            migrationBuilder.Sql(
                "CREATE INDEX ix_charge_schedules_owner_kind_owner_id " +
                "ON finance.charge_schedules (owner_kind, owner_id);");

            // The ledger's owner_type/owner_id columns are unchanged — EntityKind's ordinals match
            // the LedgerOwnerType they replace. Only the index moves, because EF no longer manages
            // it: a complex property's columns cannot be named in HasIndex.
            migrationBuilder.DropIndex(name: "ix_ledgers_owner_type_owner_id", schema: "finance", table: "ledgers");
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ix_ledgers_owner_type_owner_id " +
                "ON finance.ledgers (owner_type, owner_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "entered_by",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "owner_kind",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "owner_id",
                schema: "finance",
                table: "charge_schedules");

            migrationBuilder.DropColumn(
                name: "owner_kind",
                schema: "finance",
                table: "charge_schedules");

            migrationBuilder.RenameColumn(
                name: "owner_id",
                schema: "finance",
                table: "charges",
                newName: "user_id");

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "finance",
                table: "charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                schema: "finance",
                table: "charges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                schema: "finance",
                table: "charge_schedules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledgers_owner_type_owner_id",
                schema: "finance",
                table: "ledgers",
                columns: new[] { "owner_type", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charges_group_id_is_active",
                schema: "finance",
                table: "charges",
                columns: new[] { "group_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_charges_user_id_is_active",
                schema: "finance",
                table: "charges",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_charge_schedules_group_id",
                schema: "finance",
                table: "charge_schedules",
                column: "group_id");
        }
    }
}
