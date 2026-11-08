using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxNavi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaystackGuestCheckoutAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TaxReports",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "AccessTokenHash",
                table: "TaxReports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "TaxReports",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TaxProfiles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaymentFailedAt",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextPaymentDate",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanCode",
                table: "Subscriptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCustomerCode",
                table: "Subscriptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderEmailToken",
                table: "Subscriptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSubscriptionCode",
                table: "Subscriptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "AuthorizationUrl",
                table: "Payments",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Payments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Payments",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfilledAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanCode",
                table: "Payments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProviderTransactionId",
                table: "Payments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ProviderSubscriptionCode",
                table: "Subscriptions",
                column: "ProviderSubscriptionCode",
                unique: true,
                filter: "[ProviderSubscriptionCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TaxReportId",
                table: "Payments",
                column: "TaxReportId");

            // Data fix 1: keep only the newest pending checkout per report so the filtered unique index can be created.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY TaxReportId ORDER BY CreatedAt DESC, Id DESC) AS rn
    FROM Payments
    WHERE Status = 'Pending' AND TaxReportId IS NOT NULL
)
UPDATE Payments SET Status = 'Expired' WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1);");

            // Data fix 2: payments completed before this migration are treated as already delivered,
            // so legacy rows (including any marked completed by the old mock verifier) can never trigger fulfilment.
            migrationBuilder.Sql(@"
UPDATE Payments
SET FulfilledAt = COALESCE(PaidAt, UpdatedAt, CreatedAt)
WHERE Status = 'Completed' AND FulfilledAt IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TaxReportId_Pending",
                table: "Payments",
                column: "TaxReportId",
                unique: true,
                filter: "[Status] = 'Pending' AND [TaxReportId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_EventKey",
                table: "PaymentWebhookEvents",
                column: "EventKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_ProviderSubscriptionCode",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TaxReportId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TaxReportId_Pending",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "AccessTokenHash",
                table: "TaxReports");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "TaxReports");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "LastPaymentFailedAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "NextPaymentDate",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PlanCode",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderCustomerCode",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderEmailToken",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderSubscriptionCode",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "AuthorizationUrl",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FulfilledAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PlanCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "Payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TaxReports",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "TaxProfiles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
