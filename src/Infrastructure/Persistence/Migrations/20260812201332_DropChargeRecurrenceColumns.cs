using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropChargeRecurrenceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recurrence_end_date",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "recurrence_frequency",
                schema: "finance",
                table: "charges");

            migrationBuilder.DropColumn(
                name: "recurrence_start_date",
                schema: "finance",
                table: "charges");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "recurrence_end_date",
                schema: "finance",
                table: "charges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recurrence_frequency",
                schema: "finance",
                table: "charges",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "recurrence_start_date",
                schema: "finance",
                table: "charges",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
