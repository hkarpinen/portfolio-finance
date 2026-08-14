using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

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
                name: "bank_sync_suggestions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_transaction_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    merchant_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dismissed = table.Column<bool>(type: "boolean", nullable: false),
                    is_linked = table.Column<bool>(type: "boolean", nullable: false),
                    linked_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_sync_suggestions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "debt_terms",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    annual_percentage_rate = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    statement_day_of_month = table.Column<int>(type: "integer", nullable: true),
                    payment_due_day_of_month = table.Column<int>(type: "integer", nullable: true),
                    minimum_payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_debt_terms", x => x.id);
                });

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
                    version = table.Column<long>(type: "bigint", nullable: false),
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
                name: "financial_connections",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    institution_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    institution_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    encrypted_access_token = table.Column<string>(type: "text", nullable: false),
                    cursor = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_webhook_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_connections", x => x.id);
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
                name: "income_sources",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    recurrence_frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    recurrence_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recurrence_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tax_filing_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tax_state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    tax_federal_allowances = table.Column<int>(type: "integer", nullable: true),
                    tax_state_allowances = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    deductions = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_income_sources", x => x.id);
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
                    posted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_of_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversed_by_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_share_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledgers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "member_transfers",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_member_transfers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dead_lettered = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                schema: "finance",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    into_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    received_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_void = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
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
                name: "user_projections",
                schema: "finance",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_demo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_projections", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "financial_accounts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_account_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    official_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    mask = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subtype = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    available_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_accounts_financial_connections_financial_connecti",
                        column: x => x.financial_connection_id,
                        principalSchema: "finance",
                        principalTable: "financial_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_transactions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    authorized_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    merchant_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    primary_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detailed_category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pending = table.Column<bool>(type: "boolean", nullable: false),
                    linked_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_financial_transactions_financial_connections_financial_conn",
                        column: x => x.financial_connection_id,
                        principalSchema: "finance",
                        principalTable: "financial_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_suggestions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_stream_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    merchant_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    frequency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    first_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    predicted_next_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_linked = table.Column<bool>(type: "boolean", nullable: false),
                    linked_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    average_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    average_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    last_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    last_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_suggestions_financial_connections_financial_conne",
                        column: x => x.financial_connection_id,
                        principalSchema: "finance",
                        principalTable: "financial_connections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "ix_bank_sync_suggestions_external_transaction_id",
                schema: "finance",
                table: "bank_sync_suggestions",
                column: "external_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_sync_suggestions_user_id_dismissed",
                schema: "finance",
                table: "bank_sync_suggestions",
                columns: new[] { "user_id", "dismissed" });

            migrationBuilder.CreateIndex(
                name: "ix_debt_terms_account_id",
                schema: "finance",
                table: "debt_terms",
                column: "account_id",
                unique: true);

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
                name: "ix_financial_accounts_financial_connection_id_external_account",
                schema: "finance",
                table: "financial_accounts",
                columns: new[] { "financial_connection_id", "external_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_accounts_user_id",
                schema: "finance",
                table: "financial_accounts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_connections_external_id",
                schema: "finance",
                table: "financial_connections",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_connections_user_id",
                schema: "finance",
                table: "financial_connections",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_transactions_external_transaction_id",
                schema: "finance",
                table: "financial_transactions",
                column: "external_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_transactions_financial_connection_id_date",
                schema: "finance",
                table: "financial_transactions",
                columns: new[] { "financial_connection_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_transactions_user_id",
                schema: "finance",
                table: "financial_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_member_projections_user_id",
                schema: "finance",
                table: "group_member_projections",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_income_sources_user_id_is_active",
                schema: "finance",
                table: "income_sources",
                columns: new[] { "user_id", "is_active" });

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
                name: "ix_member_transfers_group_id_occurred_on",
                schema: "finance",
                table: "member_transfers",
                columns: new[] { "group_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_published_dead_lettered",
                schema: "finance",
                table: "outbox_messages",
                columns: new[] { "published", "dead_lettered" },
                filter: "published = false AND dead_lettered = false");

            migrationBuilder.CreateIndex(
                name: "ix_processed_events_processed_at",
                schema: "finance",
                table: "processed_events",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_received_on",
                schema: "finance",
                table: "receipts",
                column: "received_on");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_expenses_created_by",
                schema: "finance",
                table: "recurring_expenses",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_suggestions_external_stream_id",
                schema: "finance",
                table: "recurring_suggestions",
                column: "external_stream_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_suggestions_financial_connection_id",
                schema: "finance",
                table: "recurring_suggestions",
                column: "financial_connection_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_suggestions_user_id",
                schema: "finance",
                table: "recurring_suggestions",
                column: "user_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_user_projections_email",
                schema: "finance",
                table: "user_projections",
                column: "email",
                unique: true);

            // Indexed here rather than in the model: EF 8 cannot name a complex property's columns
            // in HasIndex, and every list of somebody's expenses filters on exactly these two.
            migrationBuilder.Sql(
                "CREATE INDEX ix_expenses_owner ON finance.expenses (owner_kind, owner_id, is_active);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_recurring_expenses_owner ON finance.recurring_expenses (owner_kind, owner_id);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_receipts_owner ON finance.receipts (owner_kind, owner_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "bank_sync_suggestions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "debt_terms",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expenses",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "financial_accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "financial_transactions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "group_member_projections",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "income_sources",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "journal_lines",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "ledgers",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "member_transfers",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "processed_events",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "receipts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurring_expense_terms",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurring_suggestions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "shares",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "user_projections",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurring_expenses",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "financial_connections",
                schema: "finance");
        }
    }
}
