using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_income_sources_user_id_is_active",
                schema: "finance",
                table: "income_sources",
                columns: new[] { "user_id", "is_active" });

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

            migrationBuilder.CreateIndex(
                name: "ix_expenses_due_date",
                schema: "finance",
                table: "expenses",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_household_id_is_active",
                schema: "finance",
                table: "expenses",
                columns: new[] { "household_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_user_id_is_active",
                schema: "finance",
                table: "expenses",
                columns: new[] { "user_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_splits_expense_id",
                schema: "finance",
                table: "expense_splits",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_splits_user_id",
                schema: "finance",
                table: "expense_splits",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_splits_user_id_expense_id",
                schema: "finance",
                table: "expense_splits",
                columns: new[] { "user_id", "expense_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_payments_user_id",
                schema: "finance",
                table: "expense_payments",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_income_sources_user_id_is_active",
                schema: "finance",
                table: "income_sources");

            migrationBuilder.DropIndex(
                name: "ix_household_memberships_household_id_is_active",
                schema: "finance",
                table: "household_memberships");

            migrationBuilder.DropIndex(
                name: "ix_household_memberships_user_id_is_active",
                schema: "finance",
                table: "household_memberships");

            migrationBuilder.DropIndex(
                name: "ix_expenses_due_date",
                schema: "finance",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "ix_expenses_household_id_is_active",
                schema: "finance",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "ix_expenses_user_id_is_active",
                schema: "finance",
                table: "expenses");

            migrationBuilder.DropIndex(
                name: "ix_expense_splits_expense_id",
                schema: "finance",
                table: "expense_splits");

            migrationBuilder.DropIndex(
                name: "ix_expense_splits_user_id",
                schema: "finance",
                table: "expense_splits");

            migrationBuilder.DropIndex(
                name: "ix_expense_splits_user_id_expense_id",
                schema: "finance",
                table: "expense_splits");

            migrationBuilder.DropIndex(
                name: "ix_expense_payments_user_id",
                schema: "finance",
                table: "expense_payments");
        }
    }
}
