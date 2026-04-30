using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeductionsAndTaxProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deductions",
                schema: "bills",
                table: "income_sources",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tax_federal_allowances",
                schema: "bills",
                table: "income_sources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_filing_status",
                schema: "bills",
                table: "income_sources",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tax_state_allowances",
                schema: "bills",
                table: "income_sources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_state_code",
                schema: "bills",
                table: "income_sources",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deductions",
                schema: "bills",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "tax_federal_allowances",
                schema: "bills",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "tax_filing_status",
                schema: "bills",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "tax_state_allowances",
                schema: "bills",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "tax_state_code",
                schema: "bills",
                table: "income_sources");
        }
    }
}
