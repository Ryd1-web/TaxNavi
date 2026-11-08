using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxNavi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BillingEntitlementsAndTaxYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reports created before this migration were computed with the pre-2026 PITA rules.
            migrationBuilder.DropIndex(
                name: "IX_TaxReports_UserId",
                table: "TaxReports");

            migrationBuilder.AddColumn<bool>(
                name: "IsUnlocked",
                table: "TaxReports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RuleSetId",
                table: "TaxReports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "pita-legacy");

            migrationBuilder.AddColumn<int>(
                name: "TaxYear",
                table: "TaxReports",
                type: "int",
                nullable: false,
                defaultValue: 2025);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockedAt",
                table: "TaxReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "NGN");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Payments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "PremiumReport");

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastPaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxReports_UserId_CreatedAt",
                table: "TaxReports",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Reference",
                table: "Payments",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId_CurrentPeriodEnd",
                table: "Subscriptions",
                columns: new[] { "UserId", "CurrentPeriodEnd" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_TaxReports_UserId_CreatedAt",
                table: "TaxReports");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Reference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsUnlocked",
                table: "TaxReports");

            migrationBuilder.DropColumn(
                name: "RuleSetId",
                table: "TaxReports");

            migrationBuilder.DropColumn(
                name: "TaxYear",
                table: "TaxReports");

            migrationBuilder.DropColumn(
                name: "UnlockedAt",
                table: "TaxReports");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<string>(
                name: "Reference",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_TaxReports_UserId",
                table: "TaxReports",
                column: "UserId");
        }
    }
}
