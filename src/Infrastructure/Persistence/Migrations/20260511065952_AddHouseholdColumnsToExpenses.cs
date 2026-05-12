using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdColumnsToExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "finance",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "household_id",
                schema: "finance",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transaction_reference",
                schema: "finance",
                table: "expense_payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

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
                name: "expense_split_payments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_split_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_expense_splits", x => x.id);
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_sync_suggestions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense_split_payments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "expense_splits",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "financial_accounts",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "financial_transactions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "recurring_suggestions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "financial_connections",
                schema: "finance");

            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "finance",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "household_id",
                schema: "finance",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "transaction_reference",
                schema: "finance",
                table: "expense_payments");

            migrationBuilder.CreateTable(
                name: "plaid_accounts",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    available_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    mask = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    official_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    plaid_account_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plaid_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subtype = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cursor = table.Column<string>(type: "text", nullable: true),
                    encrypted_access_token = table.Column<string>(type: "text", nullable: false),
                    institution_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    institution_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_webhook_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plaid_item_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    authorized_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    detailed_category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    linked_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    merchant_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    pending = table.Column<bool>(type: "boolean", nullable: false),
                    plaid_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    primary_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plaid_transactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recurring_streams",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    first_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    frequency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_linked = table.Column<bool>(type: "boolean", nullable: false),
                    last_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    linked_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    linked_entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    merchant_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    plaid_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plaid_stream_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    predicted_next_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    shared_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_expense_split_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    recurrence_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recurrence_frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    recurrence_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shared_expenses", x => x.id);
                });

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
        }
    }
}
