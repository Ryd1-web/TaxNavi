using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaxNavi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class WebhookPersistenceAndPaymentIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Subscriptions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Subscriptions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "NGN");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "PaymentWebhookEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "PaymentWebhookEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "PaymentWebhookEvents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "PaymentWebhookEvents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PaymentWebhookEvents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Received");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payments",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            // Events processed before this migration must not look unprocessed to the new retry worker.
            migrationBuilder.Sql("UPDATE PaymentWebhookEvents SET Status = 'Processed', Attempts = 1 WHERE ProcessedAt IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Status_ReceivedAt",
                table: "PaymentWebhookEvents",
                columns: new[] { "Status", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SubscriptionId",
                table: "Payments",
                column: "SubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Subscriptions_SubscriptionId",
                table: "Payments",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_TaxReports_TaxReportId",
                table: "Payments",
                column: "TaxReportId",
                principalTable: "TaxReports",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Subscriptions_SubscriptionId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_TaxReports_TaxReportId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentWebhookEvents_Status_ReceivedAt",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Payments_SubscriptionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "Payload",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PaymentWebhookEvents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payments");
        }
    }
}
