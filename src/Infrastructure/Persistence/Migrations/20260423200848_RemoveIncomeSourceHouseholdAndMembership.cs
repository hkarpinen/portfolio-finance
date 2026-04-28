using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIncomeSourceHouseholdAndMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "household_id",
                schema: "bills",
                table: "income_sources");

            migrationBuilder.DropColumn(
                name: "membership_id",
                schema: "bills",
                table: "income_sources");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "household_id",
                schema: "bills",
                table: "income_sources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "membership_id",
                schema: "bills",
                table: "income_sources",
                type: "uuid",
                nullable: true);
        }
    }
}
