using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LedgerRemodelAndChargeRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense_split_payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense_splits",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expenses",
                schema: "finance");

            migrationBuilder.AddColumn<string>(
                name: "notes",
                schema: "finance",
                table: "income_sources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_type = table.Column<int>(type: "integer", nullable: false),
                    parent_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "allocations",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charge_payments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charge_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funding_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recurrence_frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recurrence_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recurrence_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_member_projections",
                schema: "finance",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_member_projections", x => new { x.group_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reversal_of_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_charge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_allocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_occurrence = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_member_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_journal_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledgers",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledgers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "postings",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_postings", x => x.id);
                    table.ForeignKey(
                        name: "fk_postings_journal_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_ledger_id",
                schema: "finance",
                table: "accounts",
                column: "ledger_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_ledger_id_code",
                schema: "finance",
                table: "accounts",
                columns: new[] { "ledger_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_allocations_charge_id",
                schema: "finance",
                table: "allocations",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_user_id",
                schema: "finance",
                table: "allocations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_user_id_charge_id",
                schema: "finance",
                table: "allocations",
                columns: new[] { "user_id", "charge_id" });

            migrationBuilder.CreateIndex(
                name: "ix_charge_payments_charge_id_occurrence_date",
                schema: "finance",
                table: "charge_payments",
                columns: new[] { "charge_id", "occurrence_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_charge_payments_user_id",
                schema: "finance",
                table: "charge_payments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_charges_due_date",
                schema: "finance",
                table: "charges",
                column: "due_date");

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
                name: "ix_group_member_projections_user_id",
                schema: "finance",
                table: "group_member_projections",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_ledger_id_date",
                schema: "finance",
                table: "journal_entries",
                columns: new[] { "ledger_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_ledger_id_source",
                schema: "finance",
                table: "journal_entries",
                columns: new[] { "ledger_id", "source" },
                unique: true,
                filter: "source IS NOT NULL AND reversal_of_entry_id IS NULL AND reversed_by_entry_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_source_allocation_id_source_occurrence",
                schema: "finance",
                table: "journal_entries",
                columns: new[] { "source_allocation_id", "source_occurrence" });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_source_charge_id",
                schema: "finance",
                table: "journal_entries",
                column: "source_charge_id");

            migrationBuilder.CreateIndex(
                name: "ix_ledgers_owner_type_owner_id",
                schema: "finance",
                table: "ledgers",
                columns: new[] { "owner_type", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_postings_account_id",
                schema: "finance",
                table: "postings",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_postings_entry_id",
                schema: "finance",
                table: "postings",
                column: "entry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "charge_payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "charges",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "group_member_projections",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "ledgers",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "postings",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "finance");

            migrationBuilder.DropColumn(
                name: "notes",
                schema: "finance",
                table: "income_sources");

            migrationBuilder.CreateTable(
                name: "expense_payments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_split_payments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_split_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_split_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_splits",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_splits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    recurrence_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recurrence_frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recurrence_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_payments_expense_id_occurrence_date",
                schema: "finance",
                table: "expense_payments",
                columns: new[] { "expense_id", "occurrence_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_payments_user_id",
                schema: "finance",
                table: "expense_payments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_split_payments_expense_id_occurrence_date",
                schema: "finance",
                table: "expense_split_payments",
                columns: new[] { "expense_id", "occurrence_date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_split_payments_expense_split_id_occurrence_date",
                schema: "finance",
                table: "expense_split_payments",
                columns: new[] { "expense_split_id", "occurrence_date" },
                unique: true);

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
                name: "ix_expenses_due_date",
                schema: "finance",
                table: "expenses",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_group_id_is_active",
                schema: "finance",
                table: "expenses",
                columns: new[] { "group_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_user_id_is_active",
                schema: "finance",
                table: "expenses",
                columns: new[] { "user_id", "is_active" });
        }
    }
}
