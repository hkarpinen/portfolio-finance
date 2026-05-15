using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GroupIdRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "household_memberships",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "households",
                schema: "finance");

            migrationBuilder.DropColumn(
                name: "household_id",
                schema: "finance",
                table: "expense_splits");

            migrationBuilder.RenameColumn(
                name: "household_id",
                schema: "finance",
                table: "expenses",
                newName: "group_id");

            migrationBuilder.RenameIndex(
                name: "ix_expenses_household_id_is_active",
                schema: "finance",
                table: "expenses",
                newName: "ix_expenses_group_id_is_active");

            migrationBuilder.RenameColumn(
                name: "membership_id",
                schema: "finance",
                table: "expense_splits",
                newName: "group_id");

            migrationBuilder.RenameColumn(
                name: "household_id",
                schema: "finance",
                table: "expense_split_payments",
                newName: "group_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "group_id",
                schema: "finance",
                table: "expenses",
                newName: "household_id");

            migrationBuilder.RenameIndex(
                name: "ix_expenses_group_id_is_active",
                schema: "finance",
                table: "expenses",
                newName: "ix_expenses_household_id_is_active");

            migrationBuilder.RenameColumn(
                name: "group_id",
                schema: "finance",
                table: "expense_splits",
                newName: "membership_id");

            migrationBuilder.RenameColumn(
                name: "group_id",
                schema: "finance",
                table: "expense_split_payments",
                newName: "household_id");

            migrationBuilder.AddColumn<Guid>(
                name: "household_id",
                schema: "finance",
                table: "expense_splits",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "household_memberships",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_memberships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "households",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_households", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_household_memberships_household_id_is_active",
                schema: "finance",
                table: "household_memberships",
                columns: new[] { "household_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_household_memberships_user_id_is_active",
                schema: "finance",
                table: "household_memberships",
                columns: new[] { "user_id", "is_active" });
        }
    }
}
