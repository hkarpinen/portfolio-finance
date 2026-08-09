using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <remarks>
    /// This migration CONSOLIDATES seven earlier migrations (W2F5F8_PayerAndIncomeNotes
    /// through RenameChargePayerToUserId) that were removed from the tree. That squash
    /// left two populations of database in the wild:
    ///
    ///   A. Fresh databases — reach this point holding the pre-remodel `expense*`
    ///      tables (empty), created by InitialCreate..DemoMode.
    ///   B. Already-deployed databases — ran the seven now-deleted migrations, so they
    ///      ALREADY hold `charges` / `allocations` / the ledger tables, and their
    ///      `__EFMigrationsHistory` still lists migration ids that no longer exist on
    ///      disk. EF replays this migration against them because its own id is absent.
    ///
    /// The original generated body assumed (A) unconditionally and died on (B) with
    /// `42P01: table "expense_payments" does not exist`, taking the service down at
    /// startup. So every statement below is written idempotently: on (A) it builds the
    /// schema, on (B) it is a near no-op that only fills in what is genuinely missing.
    /// Nothing here drops a table that could hold data — the `DROP`s are guarded and
    /// only ever fire on (A), where those tables are empty.
    ///
    /// EF's fluent operations cannot emit IF EXISTS / IF NOT EXISTS, hence raw SQL.
    /// The resulting schema is identical, so the model snapshot still matches.
    /// </remarks>
    public partial class LedgerRemodelAndChargeRename : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(IdempotentUpSql);
        }

        private const string IdempotentUpSql = @"
-- ── Retire the pre-remodel tables ───────────────────────────────────────────
-- Guarded: absent on already-remodeled databases. Dropped in dependency order
-- (no CASCADE — we never want to take an unrelated dependent object with us).
DROP TABLE IF EXISTS finance.expense_payments;
DROP TABLE IF EXISTS finance.expense_split_payments;
DROP TABLE IF EXISTS finance.expense_splits;
DROP TABLE IF EXISTS finance.expenses;

ALTER TABLE finance.income_sources
    ADD COLUMN IF NOT EXISTS notes character varying(500) NULL;

-- ── Ledger + charge tables ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS finance.ledgers (
    id uuid NOT NULL,
    owner_type integer NOT NULL,
    owner_id uuid NOT NULL,
    currency character varying(3) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_ledgers PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS finance.accounts (
    id uuid NOT NULL,
    ledger_id uuid NOT NULL,
    code character varying(50) NOT NULL,
    name character varying(200) NOT NULL,
    account_type integer NOT NULL,
    parent_account_id uuid NULL,
    is_active boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_accounts PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS finance.journal_entries (
    id uuid NOT NULL,
    ledger_id uuid NOT NULL,
    date timestamp with time zone NOT NULL,
    description character varying(500) NOT NULL,
    source character varying(200) NULL,
    recorded_at timestamp with time zone NOT NULL,
    reversal_of_entry_id uuid NULL,
    reversed_by_entry_id uuid NULL,
    source_charge_id uuid NULL,
    source_allocation_id uuid NULL,
    source_occurrence timestamp with time zone NULL,
    source_member_id uuid NULL,
    CONSTRAINT pk_journal_entries PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS finance.postings (
    id uuid NOT NULL,
    entry_id uuid NOT NULL,
    account_id uuid NOT NULL,
    direction integer NOT NULL,
    amount numeric(18,2) NOT NULL,
    currency character varying(3) NOT NULL,
    CONSTRAINT pk_postings PRIMARY KEY (id)
);

-- Postgres has no ADD CONSTRAINT IF NOT EXISTS.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_postings_journal_entries_entry_id'
          AND conrelid = 'finance.postings'::regclass
    ) THEN
        ALTER TABLE finance.postings
            ADD CONSTRAINT fk_postings_journal_entries_entry_id
            FOREIGN KEY (entry_id) REFERENCES finance.journal_entries (id) ON DELETE CASCADE;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS finance.charges (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    group_id uuid NULL,
    created_by uuid NULL,
    payer_user_id uuid NULL,
    funding_source character varying(20) NOT NULL,
    title character varying(300) NOT NULL,
    description character varying(2000) NULL,
    category character varying(50) NOT NULL,
    due_date timestamp with time zone NOT NULL,
    recurrence_frequency character varying(50) NULL,
    recurrence_start_date timestamp with time zone NULL,
    recurrence_end_date timestamp with time zone NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    is_active boolean NOT NULL,
    amount numeric(18,2) NOT NULL,
    currency character varying(3) NOT NULL,
    CONSTRAINT pk_charges PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS finance.allocations (
    id uuid NOT NULL,
    charge_id uuid NOT NULL,
    group_id uuid NOT NULL,
    user_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    amount numeric(18,2) NOT NULL,
    currency character varying(3) NOT NULL,
    CONSTRAINT pk_allocations PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS finance.charge_payments (
    id uuid NOT NULL,
    charge_id uuid NOT NULL,
    user_id uuid NOT NULL,
    occurrence_date timestamp with time zone NOT NULL,
    paid_at timestamp with time zone NOT NULL,
    transaction_reference character varying(500) NULL,
    CONSTRAINT pk_charge_payments PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS finance.group_member_projections (
    group_id uuid NOT NULL,
    user_id uuid NOT NULL,
    role character varying(50) NOT NULL,
    is_active boolean NOT NULL,
    joined_at timestamp with time zone NOT NULL,
    left_at timestamp with time zone NULL,
    updated_at timestamp with time zone NOT NULL,
    CONSTRAINT pk_group_member_projections PRIMARY KEY (group_id, user_id)
);

-- ── Backfill columns on tables that already existed ─────────────────────────
-- CREATE TABLE IF NOT EXISTS is not sufficient on population (B): those tables
-- were built by the squashed chain, so any column a LATER migration in that
-- chain added can be missing. (Observed in the wild: journal_entries had
-- reversal_of_entry_id but not reversed_by_entry_id, which made the partial
-- index below fail with 42703.) Every NULLABLE target column is asserted here;
-- each is a no-op where it already exists. NOT NULL columns are deliberately
-- left to CREATE TABLE — adding one to a populated table needs a default whose
-- correct value is a domain decision, not something a migration should guess.
ALTER TABLE finance.accounts
    ADD COLUMN IF NOT EXISTS parent_account_id uuid NULL;

ALTER TABLE finance.journal_entries
    ADD COLUMN IF NOT EXISTS source character varying(200) NULL,
    ADD COLUMN IF NOT EXISTS reversal_of_entry_id uuid NULL,
    ADD COLUMN IF NOT EXISTS reversed_by_entry_id uuid NULL,
    ADD COLUMN IF NOT EXISTS source_charge_id uuid NULL,
    ADD COLUMN IF NOT EXISTS source_allocation_id uuid NULL,
    ADD COLUMN IF NOT EXISTS source_occurrence timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS source_member_id uuid NULL;

ALTER TABLE finance.charges
    ADD COLUMN IF NOT EXISTS group_id uuid NULL,
    ADD COLUMN IF NOT EXISTS created_by uuid NULL,
    ADD COLUMN IF NOT EXISTS payer_user_id uuid NULL,
    ADD COLUMN IF NOT EXISTS description character varying(2000) NULL,
    ADD COLUMN IF NOT EXISTS recurrence_frequency character varying(50) NULL,
    ADD COLUMN IF NOT EXISTS recurrence_start_date timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS recurrence_end_date timestamp with time zone NULL;

ALTER TABLE finance.charge_payments
    ADD COLUMN IF NOT EXISTS transaction_reference character varying(500) NULL;

-- ── Indexes ─────────────────────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS ix_accounts_ledger_id ON finance.accounts (ledger_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_accounts_ledger_id_code ON finance.accounts (ledger_id, code);
CREATE INDEX IF NOT EXISTS ix_allocations_charge_id ON finance.allocations (charge_id);
CREATE INDEX IF NOT EXISTS ix_allocations_user_id ON finance.allocations (user_id);
CREATE INDEX IF NOT EXISTS ix_allocations_user_id_charge_id ON finance.allocations (user_id, charge_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_charge_payments_charge_id_occurrence_date ON finance.charge_payments (charge_id, occurrence_date);
CREATE INDEX IF NOT EXISTS ix_charge_payments_user_id ON finance.charge_payments (user_id);
CREATE INDEX IF NOT EXISTS ix_charges_due_date ON finance.charges (due_date);
CREATE INDEX IF NOT EXISTS ix_charges_group_id_is_active ON finance.charges (group_id, is_active);
CREATE INDEX IF NOT EXISTS ix_charges_user_id_is_active ON finance.charges (user_id, is_active);
CREATE INDEX IF NOT EXISTS ix_group_member_projections_user_id ON finance.group_member_projections (user_id);
CREATE INDEX IF NOT EXISTS ix_journal_entries_ledger_id_date ON finance.journal_entries (ledger_id, date);
-- Partial unique index: the filter string is raw SQL, so snake_case column
-- names are required here (the naming convention does NOT rewrite it).
CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_entries_ledger_id_source ON finance.journal_entries (ledger_id, source)
    WHERE source IS NOT NULL AND reversal_of_entry_id IS NULL AND reversed_by_entry_id IS NULL;
CREATE INDEX IF NOT EXISTS ix_journal_entries_source_allocation_id_source_occurrence ON finance.journal_entries (source_allocation_id, source_occurrence);
CREATE INDEX IF NOT EXISTS ix_journal_entries_source_charge_id ON finance.journal_entries (source_charge_id);
CREATE UNIQUE INDEX IF NOT EXISTS ix_ledgers_owner_type_owner_id ON finance.ledgers (owner_type, owner_id);
CREATE INDEX IF NOT EXISTS ix_postings_account_id ON finance.postings (account_id);
CREATE INDEX IF NOT EXISTS ix_postings_entry_id ON finance.postings (entry_id);
";

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
