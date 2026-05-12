using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "expense_payments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expense_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_expenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "household_memberships",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    invitation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
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
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_households", x => x.id);
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
                name: "plaid_accounts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_account_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    official_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    mask = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subtype = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    available_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plaid_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plaid_items",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_item_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_plaid_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plaid_transactions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_plaid_transactions", x => x.id);
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
                name: "recurring_streams",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_stream_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_recurring_streams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shared_expense_split_payments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_expense_split_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shared_expense_split_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shared_expense_splits",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shared_expense_splits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shared_expenses",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_shared_expenses", x => x.id);
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
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_projections", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_expense_payments_expense_id_occurrence_date",
                schema: "finance",
                table: "expense_payments",
                columns: new[] { "expense_id", "occurrence_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_published_dead_lettered",
                schema: "finance",
                table: "outbox_messages",
                columns: new[] { "published", "dead_lettered" },
                filter: "published = false AND dead_lettered = false");

            migrationBuilder.CreateIndex(
                name: "ix_plaid_accounts_plaid_item_id_plaid_account_id",
                schema: "finance",
                table: "plaid_accounts",
                columns: new[] { "plaid_item_id", "plaid_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plaid_accounts_user_id",
                schema: "finance",
                table: "plaid_accounts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_plaid_items_plaid_item_id",
                schema: "finance",
                table: "plaid_items",
                column: "plaid_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plaid_items_user_id",
                schema: "finance",
                table: "plaid_items",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_plaid_transactions_plaid_item_id_date",
                schema: "finance",
                table: "plaid_transactions",
                columns: new[] { "plaid_item_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_plaid_transactions_plaid_transaction_id",
                schema: "finance",
                table: "plaid_transactions",
                column: "plaid_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plaid_transactions_user_id",
                schema: "finance",
                table: "plaid_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_processed_events_processed_at",
                schema: "finance",
                table: "processed_events",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_streams_plaid_stream_id",
                schema: "finance",
                table: "recurring_streams",
                column: "plaid_stream_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_streams_user_id",
                schema: "finance",
                table: "recurring_streams",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_expense_split_payments_shared_expense_id_occurrence_",
                schema: "finance",
                table: "shared_expense_split_payments",
                columns: new[] { "shared_expense_id", "occurrence_date" });

            migrationBuilder.CreateIndex(
                name: "ix_shared_expense_split_payments_shared_expense_split_id_occur",
                schema: "finance",
                table: "shared_expense_split_payments",
                columns: new[] { "shared_expense_split_id", "occurrence_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_projections_email",
                schema: "finance",
                table: "user_projections",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expenses",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "household_memberships",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "households",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "income_sources",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "plaid_accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "plaid_items",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "plaid_transactions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "processed_events",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurring_streams",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "shared_expense_split_payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "shared_expense_splits",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "shared_expenses",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "user_projections",
                schema: "finance");
        }
    }
}
