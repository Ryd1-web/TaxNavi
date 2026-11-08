using System.Text.Json;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Entities;
using Xunit;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Tests;

public class ReportCheckoutTests
{
    private readonly BillingTestHarness _h = new();

    private static CheckoutRequestDto GuestCheckout(string? email = "guest@example.com", string type = TaxpayerKinds.Individual) => new()
    {
        Purpose = PaymentPurposes.PremiumReport,
        Email = email,
        Calculation = BillingTestHarness.Calculation(type)
    };

    [Fact]
    public async Task Guest_WithoutEmail_IsRejected()
    {
        var result = await _h.Billing.CreateCheckoutAsync(null, GuestCheckout(email: null), BillingTestHarness.Urls);
        Assert.Equal(BillingOutcome.Invalid, result.Outcome);
        Assert.Empty(_h.Store.Payments.Items);
    }

    [Fact]
    public async Task Guest_Checkout_CreatesGuestReportAndPendingPayment()
    {
        var result = await _h.Billing.CreateCheckoutAsync(null, GuestCheckout("Guest@Example.com"), BillingTestHarness.Urls);

        Assert.Equal(BillingOutcome.Ok, result.Outcome);
        var report = Assert.Single(_h.Store.Reports.Items);
        var payment = Assert.Single(_h.Store.Payments.Items);
        Assert.Null(report.UserId);
        Assert.Equal("guest@example.com", report.GuestEmail);
        Assert.NotNull(result.Data!.AccessToken);
        Assert.NotEqual(result.Data.AccessToken, report.AccessTokenHash); // only the hash is stored
        Assert.True(ReportAccessTokens.Matches(result.Data.AccessToken, report.AccessTokenHash));
        Assert.Equal(report.Id, payment.TaxReportId);
        Assert.Null(payment.UserId);
        Assert.Equal("guest@example.com", payment.Email);
        Assert.Equal(PaymentStatuses.Pending, payment.Status);
        Assert.StartsWith("TNV-", payment.Reference);
    }

    [Theory]
    [InlineData(TaxpayerKinds.Individual, 3_500)]
    [InlineData(TaxpayerKinds.SME, 7_000)]
    public async Task Price_IsResolvedServerSideByReportType(string type, decimal expected)
    {
        var result = await _h.Billing.CreateCheckoutAsync(null, GuestCheckout(type: type), BillingTestHarness.Urls);

        Assert.Equal(expected, result.Data!.Amount);
        Assert.Equal(expected, _h.Gateway.Initiated.Single().Amount);
        Assert.Equal((long)(expected * 100), Money.ToKobo(expected));
    }

    [Fact]
    public async Task Checkout_SendsCallbackCancelUrlAndMetadata()
    {
        await _h.Billing.CreateCheckoutAsync(null, GuestCheckout(), BillingTestHarness.Urls);

        var request = _h.Gateway.Initiated.Single();
        var payment = _h.Store.Payments.Items.Single();
        Assert.Equal(BillingTestHarness.Urls.CallbackUrl, request.CallbackUrl);
        Assert.Contains($"reference={payment.Reference}", request.CancelUrl);
        Assert.Contains("cancelled=1", request.CancelUrl);
        Assert.Equal(payment.Id.ToString(), request.Metadata["payment_id"]);
        Assert.Equal("NGN", request.Currency);
    }

    [Fact]
    public async Task RepeatCheckout_ReusesPendingTransaction()
    {
        var first = await _h.Billing.CreateCheckoutAsync(null, GuestCheckout(), BillingTestHarness.Urls);
        var again = new CheckoutRequestDto { Purpose = PaymentPurposes.PremiumReport, Email = "guest@example.com", TaxReportId = first.Data!.TaxReportId, AccessToken = first.Data.AccessToken };

        var second = await _h.Billing.CreateCheckoutAsync(null, again, BillingTestHarness.Urls);

        Assert.True(second.Data!.Reused);
        Assert.Equal(first.Data.Reference, second.Data.Reference);
        Assert.Single(_h.Gateway.Initiated);
        Assert.Single(_h.Store.Payments.Items);
    }

    [Fact]
    public async Task StaleCheckout_IsCheckedWithPaystackThenSuperseded()
    {
        var first = await _h.Billing.CreateCheckoutAsync(null, GuestCheckout(), BillingTestHarness.Urls);
        _h.Clock.Advance(TimeSpan.FromMinutes(45));
        var again = new CheckoutRequestDto { Purpose = PaymentPurposes.PremiumReport, Email = "guest@example.com", TaxReportId = first.Data!.TaxReportId, AccessToken = first.Data.AccessToken };

        var second = await _h.Billing.CreateCheckoutAsync(null, again, BillingTestHarness.Urls);

        Assert.False(second.Data!.Reused);
        Assert.Equal(1, _h.Gateway.VerifyCalls); // old transaction checked before being superseded
        Assert.Equal(PaymentStatuses.Expired, _h.Store.Payments.Items.Single(p => p.Reference == first.Data.Reference).Status);
    }

    [Fact]
    public async Task StaleCheckout_ThatWasActuallyPaid_UnlocksInsteadOfChargingAgain()
    {
        var first = await _h.Billing.CreateCheckoutAsync(null, GuestCheckout(), BillingTestHarness.Urls);
        _h.Clock.Advance(TimeSpan.FromMinutes(45));
        _h.PaystackSays(first.Data!.Reference, "success");
        var again = new CheckoutRequestDto { Purpose = PaymentPurposes.PremiumReport, Email = "guest@example.com", TaxReportId = first.Data.TaxReportId, AccessToken = first.Data.AccessToken };

        var second = await _h.Billing.CreateCheckoutAsync(null, again, BillingTestHarness.Urls);

        Assert.Equal(BillingOutcome.AlreadyEntitled, second.Outcome);
        Assert.Single(_h.Gateway.Initiated);
        Assert.True(_h.Store.Reports.Items.Single().IsUnlocked);
    }

    [Fact]
    public async Task PaymentsNotConfigured_ReturnsUnavailableWithoutCreatingTransaction()
    {
        _h.Gateway.Configured = false;
        var result = await _h.Billing.CreateCheckoutAsync(null, GuestCheckout(), BillingTestHarness.Urls);

        Assert.Equal(BillingOutcome.PaymentsNotConfigured, result.Outcome);
        Assert.Empty(_h.Store.Payments.Items);
        Assert.Empty(_h.Gateway.Initiated);
    }

    [Fact]
    public async Task Guest_CannotCheckoutSomeoneElsesReport()
    {
        var owner = _h.AddUser();
        var report = _h.AddReport(owner.Id);
        var request = new CheckoutRequestDto { Purpose = PaymentPurposes.PremiumReport, Email = "x@example.com", TaxReportId = report.Id };

        Assert.Equal(BillingOutcome.NotFound, (await _h.Billing.CreateCheckoutAsync(null, request, BillingTestHarness.Urls)).Outcome);
        Assert.Equal(BillingOutcome.NotFound, (await _h.Billing.CreateCheckoutAsync(_h.AddUser("y@example.com").Id, request, BillingTestHarness.Urls)).Outcome);
    }

    [Fact]
    public async Task SignedInUser_UsesAccountEmail()
    {
        var user = _h.AddUser("member@example.com");
        var report = _h.AddReport(user.Id);

        await _h.Billing.CreateCheckoutAsync(user.Id, new CheckoutRequestDto { Purpose = PaymentPurposes.PremiumReport, TaxReportId = report.Id, Email = "other@example.com" }, BillingTestHarness.Urls);

        Assert.Equal("member@example.com", _h.Gateway.Initiated.Single().Email);
    }

    [Fact]
    public async Task UnlockedReport_CannotBeBoughtTwice()
    {
        var user = _h.AddUser();
        var report = _h.AddReport(user.Id);
        report.IsUnlocked = true;

        var result = await _h.Billing.CreateCheckoutAsync(user.Id, new CheckoutRequestDto { Purpose = PaymentPurposes.PremiumReport, TaxReportId = report.Id }, BillingTestHarness.Urls);

        Assert.Equal(BillingOutcome.AlreadyEntitled, result.Outcome);
        Assert.Empty(_h.Gateway.Initiated);
    }
}

public class PaymentVerificationTests
{
    private readonly BillingTestHarness _h = new();

    private async Task<CheckoutResponseDto> GuestCheckoutAsync()
    {
        var result = await _h.Billing.CreateCheckoutAsync(null, new CheckoutRequestDto
        {
            Purpose = PaymentPurposes.PremiumReport,
            Email = "guest@example.com",
            Calculation = BillingTestHarness.Calculation()
        }, BillingTestHarness.Urls);
        return result.Data!;
    }

    private TaxReport Report => _h.Store.Reports.Items.Single();

    [Fact]
    public async Task VerifiedPayment_UnlocksGuestReport_OnlyWithItsToken()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "success");

        var result = await _h.Billing.VerifyAsync(null, checkout.Reference);

        Assert.Equal(BillingOutcome.Ok, result.Outcome);
        Assert.True(Report.IsUnlocked);
        Assert.Equal(UnlockSources.Purchase, await _h.Billing.ResolveReportAccessAsync(null, Report, checkout.AccessToken));
        Assert.Null(await _h.Billing.ResolveReportAccessAsync(null, Report, "wrong-token"));
        Assert.Null(await _h.Billing.ResolveReportAccessAsync(null, Report, null));
    }

    [Theory]
    [InlineData(3_499.99, "NGN", null)] // one kobo short
    [InlineData(3_500, "USD", null)]    // wrong currency
    [InlineData(3_500, "NGN", "TNV-someone-else")] // different transaction
    public async Task Mismatched_Charges_NeverUnlock(decimal naira, string currency, string? otherReference)
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "success", naira, currency, otherReference);

        var result = await _h.Billing.VerifyAsync(null, checkout.Reference);

        Assert.NotEqual(BillingOutcome.Ok, result.Outcome);
        Assert.False(Report.IsUnlocked);
    }

    [Fact]
    public async Task AmountMismatch_IsRecordedAsFailed()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "success", 100m);

        await _h.Billing.VerifyAsync(null, checkout.Reference);

        var payment = _h.Store.Payments.Items.Single();
        Assert.Equal(PaymentStatuses.Failed, payment.Status);
        Assert.Contains("amount_mismatch", payment.FailureReason);
    }

    [Fact]
    public async Task Pending_DoesNotUnlock()
    {
        var checkout = await GuestCheckoutAsync();
        var result = await _h.Billing.VerifyAsync(null, checkout.Reference);

        Assert.Equal(BillingOutcome.Pending, result.Outcome);
        Assert.False(Report.IsUnlocked);
    }

    [Fact]
    public async Task Declined_IsFailed()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "failed");

        var result = await _h.Billing.VerifyAsync(null, checkout.Reference);

        Assert.Equal(BillingOutcome.Failed, result.Outcome);
        Assert.Equal(PaymentStatuses.Failed, _h.Store.Payments.Items.Single().Status);
    }

    [Fact]
    public async Task Abandoned_IsCancelled_ButLaterSuccessStillCompletes()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "abandoned");

        var cancelled = await _h.Billing.VerifyAsync(null, checkout.Reference);
        Assert.Equal(BillingOutcome.Cancelled, cancelled.Outcome);
        Assert.Equal(PaymentStatuses.Cancelled, _h.Store.Payments.Items.Single().Status);
        Assert.False(Report.IsUnlocked);

        _h.PaystackSays(checkout.Reference, "success");
        var completed = await _h.Billing.VerifyAsync(null, checkout.Reference);
        Assert.Equal(BillingOutcome.Ok, completed.Outcome);
        Assert.True(Report.IsUnlocked);
    }

    [Fact]
    public async Task AccountPayment_IsPrivateToItsOwner()
    {
        var owner = _h.AddUser();
        var report = _h.AddReport(owner.Id);
        var checkout = await _h.Billing.CreateCheckoutAsync(owner.Id, new CheckoutRequestDto { Purpose = PaymentPurposes.PremiumReport, TaxReportId = report.Id }, BillingTestHarness.Urls);
        _h.PaystackSays(checkout.Data!.Reference, "success");

        Assert.Equal(BillingOutcome.NotFound, (await _h.Billing.VerifyAsync(null, checkout.Data.Reference)).Outcome);
        Assert.Equal(BillingOutcome.NotFound, (await _h.Billing.VerifyAsync(Guid.NewGuid(), checkout.Data.Reference)).Outcome);
        Assert.False(report.IsUnlocked);
        Assert.Equal(BillingOutcome.Ok, (await _h.Billing.VerifyAsync(owner.Id, checkout.Data.Reference)).Outcome);
        Assert.True(report.IsUnlocked);
    }

    [Fact]
    public async Task VerifyAndWebhook_FulfilExactlyOnce()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "success");

        await _h.Billing.VerifyAsync(null, checkout.Reference);
        await _h.Billing.HandleWebhookAsync(ChargeSuccess(checkout.Reference, 1));
        await _h.Billing.VerifyAsync(null, checkout.Reference);

        Assert.Equal(1, _h.Store.Payments.FulfilmentWins);
    }

    [Fact]
    public async Task Webhook_ChargeSuccess_ReverifiesAndUnlocks()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "success");

        var result = await _h.Billing.HandleWebhookAsync(ChargeSuccess(checkout.Reference, 77));

        Assert.Equal("Ok", result.Outcome);
        Assert.True(Report.IsUnlocked);
        Assert.Equal(1, _h.Gateway.VerifyCalls);
    }

    [Fact]
    public async Task Webhook_BodyClaimingSuccess_IsNotTrustedWithoutPaystackConfirmation()
    {
        var checkout = await GuestCheckoutAsync();
        // Paystack's API still says the transaction is ongoing.
        var result = await _h.Billing.HandleWebhookAsync(ChargeSuccess(checkout.Reference, 78));

        Assert.Equal("Pending", result.Outcome);
        Assert.False(Report.IsUnlocked);
    }

    [Fact]
    public async Task DuplicateWebhook_IsProcessedOnce()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "success");
        var payload = ChargeSuccess(checkout.Reference, 99);

        var first = await _h.Billing.HandleWebhookAsync(payload);
        var second = await _h.Billing.HandleWebhookAsync(payload);

        Assert.Equal("Ok", first.Outcome);
        Assert.Equal("duplicate", second.Outcome);
        Assert.Equal(1, _h.Gateway.VerifyCalls);
    }

    [Fact]
    public async Task Webhook_UnknownReference_IsIgnored()
    {
        var result = await _h.Billing.HandleWebhookAsync(ChargeSuccess("NOT-OURS", 5));
        Assert.Equal("unknown_reference", result.Outcome);
    }

    [Fact]
    public async Task GuestReport_CanBeClaimedIntoAccount_WithToken()
    {
        var checkout = await GuestCheckoutAsync();
        _h.PaystackSays(checkout.Reference, "success");
        await _h.Billing.VerifyAsync(null, checkout.Reference);
        var user = _h.AddUser("guest@example.com");

        Assert.Equal(BillingOutcome.NotFound, (await _h.Billing.ClaimGuestReportAsync(user.Id, Report.Id, "bad")).Outcome);
        var claimed = await _h.Billing.ClaimGuestReportAsync(user.Id, Report.Id, checkout.AccessToken);

        Assert.Equal(BillingOutcome.Ok, claimed.Outcome);
        Assert.Equal(user.Id, Report.UserId);
        Assert.Null(Report.AccessTokenHash);
        Assert.Equal(user.Id, _h.Store.Payments.Items.Single().UserId);
        Assert.Equal(UnlockSources.Purchase, await _h.Billing.ResolveReportAccessAsync(user.Id, Report));
    }

    internal static string ChargeSuccess(string reference, long id, object? plan = null) => JsonSerializer.Serialize(new
    {
        @event = "charge.success",
        data = new { id, reference, status = "success", amount = 350000, currency = "NGN", customer = new { email = "guest@example.com" }, plan = plan ?? new { } }
    });
}

public class SubscriptionBillingTests
{
    private readonly BillingTestHarness _h = new();

    private async Task<(User User, string Reference)> SubscribeAsync(bool recurring = true)
    {
        if (recurring) _h.SubscriptionOptions.SmePlanCode = "PLN_sme_monthly";
        var user = _h.AddUser("biz@example.com", "SME");
        var checkout = await _h.Billing.CreateCheckoutAsync(user.Id, new CheckoutRequestDto { Purpose = PaymentPurposes.SmeSubscription }, BillingTestHarness.Urls);
        _h.PaystackSays(checkout.Data!.Reference, "success", plan: recurring ? "PLN_sme_monthly" : null);
        await _h.Billing.VerifyAsync(user.Id, checkout.Data.Reference);
        return (user, checkout.Data.Reference);
    }

    private void RemoteSubscription(string status, DateTime? next = null) => _h.Gateway.RemoteSubscriptions["SUB_abc"] = new ProviderSubscription
    {
        SubscriptionCode = "SUB_abc",
        Status = status,
        EmailToken = "tok_123",
        CustomerEmail = "biz@example.com",
        CustomerCode = "CUS_1",
        PlanCode = "PLN_sme_monthly",
        NextPaymentDate = next ?? _h.Clock.GetUtcNow().UtcDateTime.AddDays(30)
    };

    private static string Event(string name, string code) => JsonSerializer.Serialize(new
    {
        @event = name,
        data = new { subscription_code = code, status = name, customer = new { email = "biz@example.com" }, subscription = new { subscription_code = code } }
    });

    [Fact]
    public async Task Subscription_RequiresSignIn()
    {
        var result = await _h.Billing.CreateCheckoutAsync(null, new CheckoutRequestDto { Purpose = PaymentPurposes.SmeSubscription }, BillingTestHarness.Urls);
        Assert.Equal(BillingOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task RecurringCheckout_UsesPlanAndConfiguredPrice()
    {
        var (_, _) = await SubscribeAsync();

        var request = _h.Gateway.Initiated.Single();
        Assert.Equal("PLN_sme_monthly", request.PlanCode);
        Assert.Equal(7_500m, request.Amount);
    }

    [Fact]
    public async Task VerifiedFirstPayment_ActivatesRecurringPlan_AndWebhookLinksPaystackSubscription()
    {
        var (user, _) = await SubscribeAsync();
        RemoteSubscription("active", _h.Clock.GetUtcNow().UtcDateTime.AddDays(31));

        await _h.Billing.HandleWebhookAsync(Event("subscription.create", "SUB_abc"));

        var subscription = _h.Store.Subscriptions.Items.Single();
        Assert.True(subscription.IsRecurring);
        Assert.Equal("SUB_abc", subscription.ProviderSubscriptionCode);
        Assert.Equal("tok_123", subscription.ProviderEmailToken);
        Assert.Equal(SubscriptionStatuses.Active, subscription.Status);
        var entitlements = await _h.Billing.GetEntitlementsAsync(user.Id);
        Assert.True(entitlements.HasActiveSubscription);
        Assert.True(entitlements.CanCancelSubscription);
        Assert.Equal("Recurring", entitlements.SubscriptionMode);
    }

    [Fact]
    public async Task ActiveSubscription_UnlocksOwnReportsOnly()
    {
        var (user, _) = await SubscribeAsync();
        var own = _h.AddReport(user.Id, TaxpayerKinds.SME);
        var other = _h.AddReport(_h.AddUser("z@example.com").Id);

        Assert.Equal(UnlockSources.Subscription, await _h.Billing.ResolveReportAccessAsync(user.Id, own));
        Assert.Null(await _h.Billing.ResolveReportAccessAsync(user.Id, other));
    }

    [Fact]
    public async Task FailedRenewal_GivesGracePeriod_ThenLapses()
    {
        var (user, _) = await SubscribeAsync();
        RemoteSubscription("active");
        await _h.Billing.HandleWebhookAsync(Event("subscription.create", "SUB_abc"));
        var periodEnd = _h.Store.Subscriptions.Items.Single().CurrentPeriodEnd;

        RemoteSubscription("attention", periodEnd);
        await _h.Billing.HandleWebhookAsync(Event("invoice.payment_failed", "SUB_abc"));
        Assert.Equal(SubscriptionStatuses.PastDue, _h.Store.Subscriptions.Items.Single().Status);

        _h.Clock.Advance(periodEnd - _h.Clock.GetUtcNow().UtcDateTime + TimeSpan.FromDays(2));
        Assert.True((await _h.Billing.GetEntitlementsAsync(user.Id)).HasActiveSubscription); // within 3-day grace

        _h.Clock.Advance(TimeSpan.FromDays(2));
        Assert.False((await _h.Billing.GetEntitlementsAsync(user.Id)).HasActiveSubscription);
    }

    [Fact]
    public async Task Renewal_ExtendsPeriod_AndIsRecordedOnce()
    {
        var (user, _) = await SubscribeAsync();
        RemoteSubscription("active");
        await _h.Billing.HandleWebhookAsync(Event("subscription.create", "SUB_abc"));
        var before = _h.Store.Subscriptions.Items.Single().CurrentPeriodEnd;

        _h.Clock.Advance(TimeSpan.FromDays(30));
        RemoteSubscription("active", _h.Clock.GetUtcNow().UtcDateTime.AddDays(30));
        _h.PaystackSays("PSK_renewal_1", "success", 7_500m, email: "biz@example.com", plan: "PLN_sme_monthly");
        var renewal = PaymentVerificationTests.ChargeSuccess("PSK_renewal_1", 500, new { plan_code = "PLN_sme_monthly" });

        await _h.Billing.HandleWebhookAsync(renewal);
        await _h.Billing.HandleWebhookAsync(renewal);

        Assert.Single(_h.Store.Payments.Items, p => p.Reference == "PSK_renewal_1");
        Assert.True(_h.Store.Subscriptions.Items.Single().CurrentPeriodEnd > before);
        Assert.True((await _h.Billing.GetEntitlementsAsync(user.Id)).HasActiveSubscription);
    }

    [Fact]
    public async Task Cancel_DisablesAtPaystack_KeepsAccessUntilPeriodEnd()
    {
        var (user, _) = await SubscribeAsync();
        RemoteSubscription("active");
        await _h.Billing.HandleWebhookAsync(Event("subscription.create", "SUB_abc"));

        var result = await _h.Subscriptions.CancelAsync(user.Id);

        Assert.Equal(BillingOutcome.Ok, result.Outcome);
        Assert.Equal(("SUB_abc", "tok_123"), _h.Gateway.Disabled.Single());
        var subscription = _h.Store.Subscriptions.Items.Single();
        Assert.Equal(SubscriptionStatuses.NonRenewing, subscription.Status);
        Assert.True((await _h.Billing.GetEntitlementsAsync(user.Id)).HasActiveSubscription);

        _h.Clock.Advance(subscription.CurrentPeriodEnd - _h.Clock.GetUtcNow().UtcDateTime + TimeSpan.FromMinutes(1));
        Assert.False((await _h.Billing.GetEntitlementsAsync(user.Id)).HasActiveSubscription);
    }

    [Fact]
    public async Task ActiveRecurringPlan_CannotBeBoughtTwice()
    {
        var (user, _) = await SubscribeAsync();
        var again = await _h.Billing.CreateCheckoutAsync(user.Id, new CheckoutRequestDto { Purpose = PaymentPurposes.SmeSubscription }, BillingTestHarness.Urls);
        Assert.Equal(BillingOutcome.AlreadyEntitled, again.Outcome);
    }

    [Fact]
    public async Task OneTimePass_IsExtendedOncePerPayment()
    {
        var (user, reference) = await SubscribeAsync(recurring: false);
        var end = _h.Store.Subscriptions.Items.Single().CurrentPeriodEnd;

        await _h.Billing.VerifyAsync(user.Id, reference);
        await _h.Billing.HandleWebhookAsync(PaymentVerificationTests.ChargeSuccess(reference, 42));

        Assert.Equal(end, _h.Store.Subscriptions.Items.Single().CurrentPeriodEnd);
        Assert.False(_h.Store.Subscriptions.Items.Single().IsRecurring);
    }

    [Theory]
    [InlineData("active", SubscriptionStatuses.Active)]
    [InlineData("non-renewing", SubscriptionStatuses.NonRenewing)]
    [InlineData("attention", SubscriptionStatuses.PastDue)]
    [InlineData("completed", SubscriptionStatuses.Expired)]
    [InlineData("cancelled", SubscriptionStatuses.Cancelled)]
    public void ProviderStatuses_MapToLocalStatuses(string provider, string expected)
    {
        Assert.Equal(expected, SubscriptionService.MapStatus(provider, "x"));
    }
}
