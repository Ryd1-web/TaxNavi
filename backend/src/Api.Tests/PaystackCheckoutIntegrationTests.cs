using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaxNavi.Api.Tests;

public class PaystackCheckoutIntegrationTests : IClassFixture<PaystackTestFactory>
{
    private readonly PaystackTestFactory _factory;
    private readonly HttpClient _client;

    public PaystackCheckoutIntegrationTests(PaystackTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static object Salary(decimal annual) => new
    {
        taxYear = 2026,
        taxpayerType = "Individual",
        businessType = "",
        businessStructure = "",
        incomeSources = new[] { new { name = "Salary", category = "Employment", annualAmount = annual } },
        expenses = Array.Empty<object>(),
        allowableDeductions = new { },
        vat = new { annualTurnover = 0 }
    };

    private static object Company() => new
    {
        taxYear = 2026,
        taxpayerType = "SME",
        businessType = "Retail",
        businessStructure = "LimitedCompany",
        incomeSources = new[] { new { name = "Sales", category = "Business", annualAmount = 80_000_000m } },
        expenses = Array.Empty<object>(),
        allowableDeductions = new { },
        vat = new { annualTurnover = 80_000_000m }
    };

    private async Task<(string Reference, string Token, string ReportId, decimal Amount)> GuestCheckoutAsync(object calculation, string email = "guest@example.com")
    {
        var response = await _client.PostAsJsonAsync("/api/payments/checkout", new { purpose = "PremiumReport", email, calculation });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.JsonAsync()).GetProperty("data");
        return (data.GetProperty("reference").GetString()!, data.GetProperty("accessToken").GetString()!,
            data.GetProperty("taxReportId").GetString()!, data.GetProperty("amount").GetDecimal());
    }

    private async Task<JsonElement> GetGuestReportAsync(string reportId, string? token)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/reports/guest/{reportId}").WithToken(token));
        return response.StatusCode == HttpStatusCode.OK ? (await response.JsonAsync()).GetProperty("data").GetProperty("report") : default;
    }

    private static string ChargeSuccess(string reference, long id = 1) =>
        JsonSerializer.Serialize(new { @event = "charge.success", data = new { id, reference, status = "success", amount = 350000, currency = "NGN" } });

    [Fact]
    public async Task GuestPurchase_EndToEnd_UnlocksOnlyAfterSignedWebhookAndVerification()
    {
        var (reference, token, reportId, amount) = await GuestCheckoutAsync(Salary(5_000_000m));

        Assert.Equal(3_500m, amount);
        var tx = _factory.Paystack.Transactions[reference];
        Assert.Equal(350_000, tx.AmountKobo);
        Assert.Equal("NGN", tx.Currency);
        Assert.Equal("guest@example.com", tx.Email);
        Assert.Contains("cancelled=1", tx.CancelAction);
        Assert.Contains($"Bearer {PaystackTestFactory.SecretKey}", _factory.Paystack.AuthorizationHeaders);

        var locked = await GetGuestReportAsync(reportId, token);
        Assert.False(locked.GetProperty("accessGranted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, locked.GetProperty("fullReport").ValueKind);
        Assert.Equal(default, await GetGuestReportAsync(reportId, null));
        Assert.Equal(default, await GetGuestReportAsync(reportId, "wrong-token"));

        var pdfBefore = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/reports/guest/{reportId}/pdf").WithToken(token));
        Assert.Equal(HttpStatusCode.PaymentRequired, pdfBefore.StatusCode);

        _factory.Paystack.Pay(reference);
        var webhook = await _factory.SendWebhookAsync(_client, ChargeSuccess(reference));
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        Assert.Equal("Ok", (await webhook.JsonAsync()).GetProperty("outcome").GetString());

        var unlocked = await GetGuestReportAsync(reportId, token);
        Assert.True(unlocked.GetProperty("accessGranted").GetBoolean());
        Assert.Equal(JsonValueKind.Object, unlocked.GetProperty("fullReport").ValueKind);

        var pdf = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/reports/guest/{reportId}/pdf").WithToken(token));
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);

        var verify = await _client.PostAsJsonAsync("/api/payments/verify", new { reference });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        Assert.Equal("Completed", (await verify.JsonAsync()).GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_IsRejectedAndChangesNothing()
    {
        var (reference, token, reportId, _) = await GuestCheckoutAsync(Salary(4_000_000m));
        _factory.Paystack.Pay(reference);

        var forged = await _factory.SendWebhookAsync(_client, ChargeSuccess(reference), signature: new string('a', 128));
        var unsigned = await _client.PostAsync("/api/payments/webhook/paystack", new StringContent(ChargeSuccess(reference)));

        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unsigned.StatusCode);
        Assert.False((await GetGuestReportAsync(reportId, token)).GetProperty("accessGranted").GetBoolean());
    }

    [Fact]
    public async Task Webhook_SignatureMustMatchRawBodyExactly()
    {
        var (reference, token, reportId, _) = await GuestCheckoutAsync(Salary(4_100_000m));
        _factory.Paystack.Pay(reference);
        var payload = ChargeSuccess(reference);
        var signatureForCompactBody = PaystackTestFactory.Sign(payload);

        // Same JSON, different bytes (whitespace): the signature no longer matches.
        var response = await _factory.SendWebhookAsync(_client, payload.Replace(",", ", "), signatureForCompactBody);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False((await GetGuestReportAsync(reportId, token)).GetProperty("accessGranted").GetBoolean());
    }

    [Fact]
    public async Task DuplicateWebhook_IsAcknowledgedButProcessedOnce()
    {
        var (reference, _, _, _) = await GuestCheckoutAsync(Salary(4_200_000m));
        _factory.Paystack.Pay(reference);
        var payload = ChargeSuccess(reference, id: 4242);

        var first = await _factory.SendWebhookAsync(_client, payload);
        var second = await _factory.SendWebhookAsync(_client, payload);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("Ok", (await first.JsonAsync()).GetProperty("outcome").GetString());
        Assert.Equal("duplicate", (await second.JsonAsync()).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Verify_AbandonedCheckout_ReportsCancelled()
    {
        var (reference, token, reportId, _) = await GuestCheckoutAsync(Salary(4_300_000m));
        _factory.Paystack.Transactions[reference].Status = "abandoned";

        var response = await _client.PostAsJsonAsync("/api/payments/verify", new { reference });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal("PAYMENT_CANCELLED", (await response.JsonAsync()).GetProperty("code").GetString());
        Assert.False((await GetGuestReportAsync(reportId, token)).GetProperty("accessGranted").GetBoolean());
    }

    [Fact]
    public async Task Verify_Pending_Returns202()
    {
        var (reference, _, _, _) = await GuestCheckoutAsync(Salary(4_350_000m));
        var response = await _client.PostAsJsonAsync("/api/payments/verify", new { reference });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Verify_UnderpaidTransaction_FailsAndStaysLocked()
    {
        var (reference, token, reportId, _) = await GuestCheckoutAsync(Salary(4_400_000m));
        _factory.Paystack.Pay(reference, amountKobo: 10_000); // ₦100 instead of ₦3,500

        var response = await _client.PostAsJsonAsync("/api/payments/verify", new { reference });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal("PAYMENT_FAILED", (await response.JsonAsync()).GetProperty("code").GetString());
        Assert.False((await GetGuestReportAsync(reportId, token)).GetProperty("accessGranted").GetBoolean());
    }

    [Fact]
    public async Task SmeReport_IsCharged7000Naira()
    {
        var (reference, _, _, amount) = await GuestCheckoutAsync(Company());

        Assert.Equal(7_000m, amount);
        Assert.Equal(700_000, _factory.Paystack.Transactions[reference].AmountKobo);
    }

    [Fact]
    public async Task RepeatCheckout_ReusesTheOpenTransaction()
    {
        var (reference, token, reportId, _) = await GuestCheckoutAsync(Salary(4_500_000m));

        var again = await _client.PostAsJsonAsync("/api/payments/checkout",
            new { purpose = "PremiumReport", email = "guest@example.com", taxReportId = reportId, accessToken = token });
        var data = (await again.JsonAsync()).GetProperty("data");

        Assert.Equal(reference, data.GetProperty("reference").GetString());
        Assert.True(data.GetProperty("reused").GetBoolean());
    }

    [Fact]
    public async Task GuestCheckout_RequiresValidEmail()
    {
        var missing = await _client.PostAsJsonAsync("/api/payments/checkout", new { purpose = "PremiumReport", calculation = Salary(1_000_000m) });
        var invalid = await _client.PostAsJsonAsync("/api/payments/checkout", new { purpose = "PremiumReport", email = "not-an-email", calculation = Salary(1_000_000m) });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task ClientCannotSetPrice()
    {
        var response = await _client.PostAsJsonAsync("/api/payments/checkout",
            new { purpose = "PremiumReport", email = "guest@example.com", amount = 1, calculation = Salary(2_000_000m) });
        var reference = (await response.JsonAsync()).GetProperty("data").GetProperty("reference").GetString()!;

        Assert.Equal(350_000, _factory.Paystack.Transactions[reference].AmountKobo);
    }

    [Fact]
    public async Task Verify_UnknownReference_IsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/payments/verify", new { reference = "TNV-does-not-exist" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SignedInPurchase_AndGuestClaim_WorkThroughTheApi()
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = $"member{Guid.NewGuid():N}@example.com", password = "Passw0rd!", firstName = "Ada", lastName = "Obi", userType = "Individual" });
        var jwt = (await register.JsonAsync()).GetProperty("data").GetProperty("jwtToken").GetString()!;

        // Signed-in purchase of a saved report.
        var save = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/tax/reports") { Content = JsonContent.Create(Salary(6_000_000m)) }.WithToken(bearer: jwt));
        var ownReportId = (await save.JsonAsync()).GetProperty("data").GetProperty("reportId").GetString()!;
        var checkout = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/payments/checkout")
        { Content = JsonContent.Create(new { purpose = "PremiumReport", taxReportId = ownReportId }) }.WithToken(bearer: jwt));
        var reference = (await checkout.JsonAsync()).GetProperty("data").GetProperty("reference").GetString()!;
        _factory.Paystack.Pay(reference);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/payments/verify", new { reference })).StatusCode); // anonymous caller
        var verify = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/payments/verify") { Content = JsonContent.Create(new { reference }) }.WithToken(bearer: jwt));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var own = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/api/reports/{ownReportId}").WithToken(bearer: jwt));
        Assert.True((await own.JsonAsync()).GetProperty("data").GetProperty("report").GetProperty("accessGranted").GetBoolean());

        // A guest purchase made before signing up can be claimed into the account.
        var (guestReference, token, guestReportId, _) = await GuestCheckoutAsync(Salary(3_000_000m));
        _factory.Paystack.Pay(guestReference);
        await _factory.SendWebhookAsync(_client, ChargeSuccess(guestReference, 9001));

        var claim = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/reports/{guestReportId}/claim").WithToken(token, jwt));
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var list = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/reports").WithToken(bearer: jwt));
        var ids = (await list.JsonAsync()).GetProperty("data").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToList();
        Assert.Contains(guestReportId, ids);
        var history = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/payments/history").WithToken(bearer: jwt));
        Assert.Equal(2, (await history.JsonAsync()).GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task Rules_ExposeServerPricesForTheFrontend()
    {
        var rules = await (await _client.GetAsync("/api/tax/rules")).JsonAsync();
        var prices = rules.GetProperty("data").GetProperty("monetization").GetProperty("reportPrices");

        Assert.Equal(3500, prices.GetProperty("Individual").GetDecimal());
        Assert.Equal(7000, prices.GetProperty("SME").GetDecimal());
    }
}
