using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropDebtTermsUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_debt_terms_user_id",
                schema: "finance",
                table: "debt_terms");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "finance",
                table: "debt_terms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "finance",
                table: "debt_terms",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_debt_terms_user_id",
                schema: "finance",
                table: "debt_terms",
                column: "user_id");
        }
    }
}
