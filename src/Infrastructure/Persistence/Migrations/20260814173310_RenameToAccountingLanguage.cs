using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameToAccountingLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT data-preserving. charges, allocations, charge_schedules and postings are
            // dropped and recreated under their new names rather than renamed, so every journal
            // entry loses the document it was posted from. An orphaned entry is worse than no
            // entry — the books cannot be re-derived from documents that no longer exist — so the
            // ledger goes with them. Deployed databases hold no real data; reset the volume.
            migrationBuilder.Sql("TRUNCATE finance.journal_entries CASCADE;");

            migrationBuilder.DropTable(
                name: "allocations",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "charge_schedule_amounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "charges",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "postings",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "charge_schedules",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "ix_journal_entries_source_allocation_id_source_occurrence",
                schema: "finance",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "ix_journal_entries_source_charge_id",
                schema: "finance",
                table: "journal_entries");

            migrationBuilder.RenameColumn(
                name: "source_charge_id",
                schema: "finance",
                table: "journal_entries",
                newName: "source_expense_id");

            migrationBuilder.RenameColumn(
                name: "source_allocation_id",
                schema: "finance",
                table: "journal_entries",
                newName: "source_share_id");

            migrationBuilder.CreateTable(
                name: "expenses",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entered_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recurring_expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funding_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funding_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_lines",
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
                    table.PrimaryKey("pk_journal_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_journal_lines_journal_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "finance",
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expenses",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funding_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anchor_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_expenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shares",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shares", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expense_terms",
                schema: "finance",
                columns: table => new
                {
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recurring_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_expense_terms", x => new { x.recurring_expense_id, x.effective_from });
                    table.ForeignKey(
                        name: "fk_recurring_expense_terms_recurring_expenses_recurring_expens",
                        column: x => x.recurring_expense_id,
                        principalSchema: "finance",
                        principalTable: "recurring_expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_source_expense_id",
                schema: "finance",
                table: "journal_entries",
                column: "source_expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_source_share_id_source_occurrence",
                schema: "finance",
                table: "journal_entries",
                columns: new[] { "source_share_id", "source_occurrence" });

            migrationBuilder.CreateIndex(
                name: "ix_expenses_due_date",
                schema: "finance",
                table: "expenses",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_recurring_expense_id_occurrence_date",
                schema: "finance",
                table: "expenses",
                columns: new[] { "recurring_expense_id", "occurrence_date" },
                unique: true,
                filter: "recurring_expense_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_journal_lines_account_id",
                schema: "finance",
                table: "journal_lines",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_journal_lines_entry_id",
                schema: "finance",
                table: "journal_lines",
                column: "entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_expenses_created_by",
                schema: "finance",
                table: "recurring_expenses",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_shares_expense_id",
                schema: "finance",
                table: "shares",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_shares_user_id",
                schema: "finance",
                table: "shares",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_shares_user_id_expense_id",
                schema: "finance",
                table: "shares",
                columns: new[] { "user_id", "expense_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expenses",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "journal_lines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurring_expense_terms",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "shares",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurring_expenses",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "ix_journal_entries_source_expense_id",
                schema: "finance",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "ix_journal_entries_source_share_id_source_occurrence",
                schema: "finance",
                table: "journal_entries");

            migrationBuilder.RenameColumn(
                name: "source_share_id",
                schema: "finance",
                table: "journal_entries",
                newName: "source_allocation_id");

            migrationBuilder.RenameColumn(
                name: "source_expense_id",
                schema: "finance",
                table: "journal_entries",
                newName: "source_charge_id");

            migrationBuilder.CreateTable(
                name: "allocations",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    charge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charge_schedules",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    funding_source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_kind = table.Column<int>(type: "integer", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    anchor_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charge_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "charges",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    entered_by = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    funding_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_charges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "postings",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<int>(type: "integer", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "charge_schedule_amounts",
                schema: "finance",
                columns: table => new
                {
                    schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                name: "ix_charge_schedules_created_by",
                schema: "finance",
                table: "charge_schedules",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_charges_due_date",
                schema: "finance",
                table: "charges",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_charges_schedule_id_occurrence_date",
                schema: "finance",
                table: "charges",
                columns: new[] { "schedule_id", "occurrence_date" },
                unique: true,
                filter: "schedule_id IS NOT NULL");

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
    }
}
