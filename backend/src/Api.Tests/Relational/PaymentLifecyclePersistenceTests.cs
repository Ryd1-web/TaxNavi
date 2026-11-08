using Microsoft.EntityFrameworkCore;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Models;
using Xunit;

namespace TaxNavi.Api.Tests.Relational;

/// <summary>
/// Every lifecycle stage is asserted by re-reading the database through a fresh DbContext,
/// proving the rows were actually written — not just changed in memory.
/// </summary>
public class PaymentLifecyclePersistenceTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _db;

    public PaymentLifecyclePersistenceTests(SqlServerFixture db) => _db = db;

    [SqlServerFact]
    public async Task ReportPurchase_PersistsEveryStage_AndFulfilsOnce()
    {
        var gateway = new SqlTestGateway();
        string reference;
        await using (var db = _db.NewContext())
        {
            var checkout = await _db.Services(db, gateway).Billing.CreateCheckoutAsync(null, TestData.GuestCheckout(), TestData.Urls);
            reference = checkout.Data!.Reference;
        }

        // A. Initialization
        await using (var check = _db.NewContext())
        {
            var payment = await check.Payments.SingleAsync(p => p.Reference == reference);
            var report = await check.TaxReports.SingleAsync(r => r.Id == payment.TaxReportId);
            Assert.Equal(PaymentStatuses.Pending, payment.Status);
            Assert.Equal(3_500m, payment.Amount);
            Assert.Equal("NGN", payment.Currency);
            Assert.Equal("guest@example.com", payment.Email);
            Assert.StartsWith("TNV-", payment.Reference);
            Assert.NotNull(payment.AuthorizationUrl);
            Assert.True(payment.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
            Assert.True(await check.TaxProfiles.AnyAsync(p => p.Id == report.TaxProfileId));
            Assert.False(report.IsUnlocked);
        }

        // B. Verification
        gateway.Paid(reference, 3_500m, "guest@example.com");
        await using (var db = _db.NewContext())
        {
            Assert.Equal(BillingOutcome.Ok, (await _db.Services(db, gateway).Billing.VerifyAsync(null, reference)).Outcome);
        }

        DateTime fulfilledAt;
        await using (var check = _db.NewContext())
        {
            var payment = await check.Payments.SingleAsync(p => p.Reference == reference);
            var report = await check.TaxReports.SingleAsync(r => r.Id == payment.TaxReportId);
            Assert.Equal(PaymentStatuses.Completed, payment.Status);
            Assert.NotNull(payment.ProviderTransactionId);
            Assert.NotNull(payment.VerifiedAt);
            Assert.NotNull(payment.PaidAt);
            Assert.Equal("card", payment.Channel);
            Assert.True(report.IsUnlocked);
            Assert.NotNull(report.UnlockedAt);
            fulfilledAt = payment.FulfilledAt!.Value;
        }

        // D. Duplicate verification: no second fulfilment, nothing rewritten
        await using (var db = _db.NewContext())
        {
            await _db.Services(db, gateway).Billing.VerifyAsync(null, reference);
        }
        await using (var check = _db.NewContext())
        {
            Assert.Equal(fulfilledAt, (await check.Payments.SingleAsync(p => p.Reference == reference)).FulfilledAt);
        }
    }

    [Theory]
    [InlineData("failed", PaymentStatuses.Failed)]
    [InlineData("abandoned", PaymentStatuses.Cancelled)]
    public async Task FailedOrAbandonedPayment_IsRecorded_WithoutGrantingAccess(string providerStatus, string expected)
    {
        if (!SqlServerAvailable.IsAvailable) return;

        var gateway = new SqlTestGateway();
        string reference;
        await using (var db = _db.NewContext())
        {
            reference = (await _db.Services(db, gateway).Billing.CreateCheckoutAsync(null, TestData.GuestCheckout(), TestData.Urls)).Data!.Reference;
        }

        gateway.Paid(reference, 3_500m, "guest@example.com", status: providerStatus);
        await using (var db = _db.NewContext())
        {
            await _db.Services(db, gateway).Billing.VerifyAsync(null, reference);
        }

        await using var check = _db.NewContext();
        var payment = await check.Payments.SingleAsync(p => p.Reference == reference);
        Assert.Equal(expected, payment.Status);
        Assert.Null(payment.FulfilledAt);
        Assert.False((await check.TaxReports.SingleAsync(r => r.Id == payment.TaxReportId)).IsUnlocked);
    }

    [SqlServerFact]
    public async Task Webhook_IsPersistedOnReceipt_ThenMarkedProcessed_AndDuplicatesIgnored()
    {
        var gateway = new SqlTestGateway();
        string reference;
        await using (var db = _db.NewContext())
        {
            reference = (await _db.Services(db, gateway).Billing.CreateCheckoutAsync(null, TestData.GuestCheckout(), TestData.Urls)).Data!.Reference;
        }
        gateway.Paid(reference, 3_500m, "guest@example.com");
        var payload = TestData.Event("charge.success", new { id = 555001, reference, status = "success", amount = 350000, currency = "NGN" });

        await using (var db = _db.NewContext())
        {
            var billing = _db.Services(db, gateway).Billing;
            Assert.Equal("Ok", (await billing.HandleWebhookAsync(payload)).Outcome);
            Assert.Equal("duplicate", (await billing.HandleWebhookAsync(payload)).Outcome);
        }

        await using var check = _db.NewContext();
        var stored = await check.PaymentWebhookEvents.Where(e => e.Reference == reference).ToListAsync();
        var record = Assert.Single(stored);
        Assert.Equal("charge.success", record.EventType);
        Assert.Equal(WebhookEventStatuses.Processed, record.Status);
        Assert.Equal(payload, record.Payload);
        Assert.Equal(1, record.Attempts);
        Assert.NotNull(record.ProcessedAt);
        Assert.Null(record.LastError);
        Assert.Equal(PaymentStatuses.Completed, (await check.Payments.SingleAsync(p => p.Reference == reference)).Status);
    }

    [SqlServerFact]
    public async Task WebhookProcessingFailure_IsRecorded_ThenRecoveredByRetry()
    {
        var gateway = new SqlTestGateway();
        string reference;
        await using (var db = _db.NewContext())
        {
            reference = (await _db.Services(db, gateway).Billing.CreateCheckoutAsync(null, TestData.GuestCheckout(), TestData.Urls)).Data!.Reference;
        }
        gateway.Paid(reference, 3_500m, "guest@example.com");
        gateway.FailNextVerifications = 1;
        var payload = TestData.Event("charge.success", new { id = 555002, reference, status = "success" });

        await using (var db = _db.NewContext())
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => _db.Services(db, gateway).Billing.HandleWebhookAsync(payload));
        }

        await using (var check = _db.NewContext())
        {
            var failed = await check.PaymentWebhookEvents.SingleAsync(e => e.Reference == reference);
            Assert.Equal(WebhookEventStatuses.Failed, failed.Status);
            Assert.Null(failed.ProcessedAt); // never marked processed before the work succeeded
            Assert.Contains("HttpRequestException", failed.LastError);
            Assert.Equal(PaymentStatuses.Pending, (await check.Payments.SingleAsync(p => p.Reference == reference)).Status);
        }

        await using (var db = _db.NewContext())
        {
            Assert.True(await _db.Services(db, gateway).Billing.RetryPendingWebhooksAsync(8, TimeSpan.Zero, 50) >= 1);
        }

        await using var final = _db.NewContext();
        var recovered = await final.PaymentWebhookEvents.SingleAsync(e => e.Reference == reference);
        Assert.Equal(WebhookEventStatuses.Processed, recovered.Status);
        Assert.Equal(2, recovered.Attempts);
        Assert.Null(recovered.LastError);
        Assert.Equal(PaymentStatuses.Completed, (await final.Payments.SingleAsync(p => p.Reference == reference)).Status);
    }

    [SqlServerFact]
    public async Task Subscription_Lifecycle_IsPersistedEndToEnd()
    {
        const string plan = "PLN_sme_monthly";
        const string code = "SUB_lifecycle";
        var gateway = new SqlTestGateway();
        var user = TestData.NewUser("SME");
        await using (var db = _db.NewContext())
        {
            await _db.Services(db, gateway).Users.AddAsync(user);
        }

        // Creation: first payment verified -> local subscription inserted and linked to the payment.
        string firstReference;
        await using (var db = _db.NewContext())
        {
            var checkout = await _db.Services(db, gateway, plan).Billing.CreateCheckoutAsync(user.Id, new CheckoutRequestDto { Purpose = PaymentPurposes.SmeSubscription }, TestData.Urls);
            firstReference = checkout.Data!.Reference;
        }
        gateway.Paid(firstReference, 7_500m, user.Email, plan);
        await using (var db = _db.NewContext())
        {
            Assert.Equal(BillingOutcome.Ok, (await _db.Services(db, gateway, plan).Billing.VerifyAsync(user.Id, firstReference)).Outcome);
        }

        Guid subscriptionId;
        await using (var check = _db.NewContext())
        {
            var subscription = await check.Subscriptions.SingleAsync(s => s.UserId == user.Id);
            var payment = await check.Payments.SingleAsync(p => p.Reference == firstReference);
            Assert.Equal(SubscriptionStatuses.Active, subscription.Status);
            Assert.True(subscription.IsRecurring);
            Assert.Equal(plan, subscription.PlanCode);
            Assert.Equal(7_500m, subscription.Amount);
            Assert.Equal(subscription.Id, payment.SubscriptionId); // FK-backed link
            subscriptionId = subscription.Id;
        }

        // Paystack confirms the subscription.
        var nextBilling = DateTime.UtcNow.AddDays(30);
        gateway.RemoteSubscriptions[code] = new ProviderSubscription
        {
            SubscriptionCode = code, Status = "active", EmailToken = "tok_lifecycle", CustomerEmail = user.Email,
            CustomerCode = "CUS_1", PlanCode = plan, AmountKobo = 750_000, NextPaymentDate = nextBilling
        };
        await using (var db = _db.NewContext())
        {
            await _db.Services(db, gateway, plan).Billing.HandleWebhookAsync(TestData.Event("subscription.create",
                new { subscription_code = code, status = "active", customer = new { email = user.Email } }));
        }

        // Successful renewal (Paystack-generated reference).
        var renewedUntil = DateTime.UtcNow.AddDays(60);
        gateway.RemoteSubscriptions[code].NextPaymentDate = renewedUntil;
        gateway.Paid("PSK_renewal_1", 7_500m, user.Email, plan);
        await using (var db = _db.NewContext())
        {
            await _db.Services(db, gateway, plan).Billing.HandleWebhookAsync(TestData.Event("charge.success",
                new { id = 777001, reference = "PSK_renewal_1", status = "success", plan = new { plan_code = plan }, customer = new { email = user.Email } }));
        }

        await using (var check = _db.NewContext())
        {
            var subscription = await check.Subscriptions.SingleAsync(s => s.Id == subscriptionId);
            var renewal = await check.Payments.SingleAsync(p => p.Reference == "PSK_renewal_1");
            Assert.Equal(code, subscription.ProviderSubscriptionCode);
            Assert.Equal("tok_lifecycle", subscription.ProviderEmailToken);
            Assert.Equal(renewedUntil, subscription.NextPaymentDate!.Value, TimeSpan.FromSeconds(1));
            Assert.True(subscription.CurrentPeriodEnd >= renewedUntil.AddSeconds(-1));
            Assert.Equal(PaymentStatuses.Completed, renewal.Status);
            Assert.Equal(subscriptionId, renewal.SubscriptionId);
            Assert.Equal(subscription.LastPaymentId, renewal.Id);
        }

        // Failed renewal: status follows Paystack ("attention" -> PastDue).
        gateway.RemoteSubscriptions[code].Status = "attention";
        await using (var db = _db.NewContext())
        {
            await _db.Services(db, gateway, plan).Billing.HandleWebhookAsync(TestData.Event("invoice.payment_failed",
                new { invoice_code = "INV_1", status = "failed", subscription = new { subscription_code = code }, customer = new { email = user.Email } }));
        }
        await using (var check = _db.NewContext())
        {
            var subscription = await check.Subscriptions.SingleAsync(s => s.Id == subscriptionId);
            Assert.Equal(SubscriptionStatuses.PastDue, subscription.Status);
            Assert.NotNull(subscription.LastPaymentFailedAt);
        }

        // Cancellation: disabled at Paystack, recorded locally, history preserved.
        gateway.RemoteSubscriptions[code].Status = "active";
        await using (var db = _db.NewContext())
        {
            Assert.Equal(BillingOutcome.Ok, (await _db.Services(db, gateway, plan).Subscriptions.CancelAsync(user.Id)).Outcome);
        }
        await using (var check = _db.NewContext())
        {
            var subscription = await check.Subscriptions.SingleAsync(s => s.Id == subscriptionId);
            Assert.Equal(SubscriptionStatuses.NonRenewing, subscription.Status);
            Assert.NotNull(subscription.CancelledAt);
            Assert.Equal(2, await check.Payments.CountAsync(p => p.SubscriptionId == subscriptionId));
            Assert.Equal(3, await check.PaymentWebhookEvents.CountAsync(e => e.Status == WebhookEventStatuses.Processed &&
                (e.Reference == code || e.Reference == "PSK_renewal_1")));
        }
    }

    [SqlServerFact]
    public async Task DeletingAReport_KeepsPaymentHistory_AndRemovesItsProfile()
    {
        var gateway = new SqlTestGateway();
        string reference;
        Guid reportId, profileId;
        await using (var db = _db.NewContext())
        {
            var checkout = await _db.Services(db, gateway).Billing.CreateCheckoutAsync(null, TestData.GuestCheckout(), TestData.Urls);
            reference = checkout.Data!.Reference;
            reportId = checkout.Data.TaxReportId!.Value;
        }
        gateway.Paid(reference, 3_500m, "guest@example.com");
        await using (var db = _db.NewContext())
        {
            var services = _db.Services(db, gateway);
            await services.Billing.VerifyAsync(null, reference);
            var report = (await services.ReportRepository.GetByIdAsync(reportId))!;
            profileId = report.TaxProfileId;
            await services.Reports.DeleteAsync(report);
        }

        await using var check = _db.NewContext();
        Assert.False(await check.TaxReports.AnyAsync(r => r.Id == reportId));
        Assert.False(await check.TaxProfiles.AnyAsync(p => p.Id == profileId)); // no orphaned calculation inputs
        var payment = await check.Payments.SingleAsync(p => p.Reference == reference);
        Assert.Equal(PaymentStatuses.Completed, payment.Status);   // financial history kept
        Assert.Null(payment.TaxReportId);
    }
}
