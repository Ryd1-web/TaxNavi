using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TaxNavi.Infrastructure.Services;
using Xunit;
using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Tests;

public class PaystackPaymentServiceTests
{
    private const string TestKey = "sk_test_unit_0000000000000000";

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Auth, string? Body)> Requests { get; } = new();
        public Func<HttpRequestMessage, string> Respond { get; set; } = _ => "{\"status\":true,\"message\":\"ok\",\"data\":{}}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(), body));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respond(request), Encoding.UTF8, "application/json") };
        }
    }

    private static (PaystackPaymentService Service, StubHandler Handler) Create(string? key = TestKey, bool allowLive = false)
    {
        var handler = new StubHandler();
        var service = new PaystackPaymentService(new HttpClient(handler), new PaystackOptions { SecretKey = key, AllowLiveKeys = allowLive, BaseUrl = "https://paystack.stub/" },
            NullLogger<PaystackPaymentService>.Instance);
        return (service, handler);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("", false, false)]
    [InlineData("sk_test_your_test_secret_key", false, false)]
    [InlineData("pk_test_abc", false, false)]
    [InlineData(TestKey, true, true)]
    [InlineData("sk_live_abc", false, false)]
    public void KeyHandling_StartsInTestModeAndRefusesLiveKeys(string? key, bool configured, bool testMode)
    {
        var (service, _) = Create(key);
        Assert.Equal(configured, service.IsConfigured);
        Assert.Equal(testMode, service.IsTestMode);
    }

    [Fact]
    public void LiveKey_RequiresExplicitOptIn()
    {
        var (service, _) = Create("sk_live_abc", allowLive: true);
        Assert.True(service.IsConfigured);
        Assert.False(service.IsTestMode);
    }

    [Fact]
    public async Task Initialize_SendsKoboAmountCallbackCancelActionAndBearerKey()
    {
        var (service, handler) = Create();
        handler.Respond = _ => "{\"status\":true,\"data\":{\"authorization_url\":\"https://checkout.paystack.com/abc\",\"access_code\":\"abc\",\"reference\":\"TNV-1\"}}";

        var result = await service.InitiatePaymentAsync(new PaymentRequest
        {
            Amount = 3_500m,
            Email = "guest@example.com",
            Reference = "TNV-1",
            CallbackUrl = "https://app/checkout/callback",
            CancelUrl = "https://app/checkout/callback?reference=TNV-1&cancelled=1",
            Metadata = new() { ["payment_id"] = "p1" }
        });

        Assert.True(result.Success);
        Assert.Equal("https://checkout.paystack.com/abc", result.AuthorizationUrl);
        var request = handler.Requests.Single();
        Assert.Equal("/transaction/initialize", request.Path);
        Assert.Equal($"Bearer {TestKey}", request.Auth);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("350000", body.RootElement.GetProperty("amount").GetString());
        Assert.Equal("NGN", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal("TNV-1", body.RootElement.GetProperty("reference").GetString());
        Assert.Equal("https://app/checkout/callback", body.RootElement.GetProperty("callback_url").GetString());
        Assert.Equal("https://app/checkout/callback?reference=TNV-1&cancelled=1", body.RootElement.GetProperty("metadata").GetProperty("cancel_action").GetString());
        Assert.False(body.RootElement.TryGetProperty("plan", out _));
    }

    [Fact]
    public async Task Initialize_WithPlan_SendsPlanCode()
    {
        var (service, handler) = Create();
        handler.Respond = _ => "{\"status\":true,\"data\":{\"authorization_url\":\"https://x\",\"reference\":\"TNV-2\"}}";

        await service.InitiatePaymentAsync(new PaymentRequest { Amount = 7_500m, Email = "a@b.co", Reference = "TNV-2", PlanCode = "PLN_x" });

        using var body = JsonDocument.Parse(handler.Requests.Single().Body!);
        Assert.Equal("PLN_x", body.RootElement.GetProperty("plan").GetString());
    }

    [Fact]
    public async Task Initialize_ProviderRejection_IsNotSuccess()
    {
        var (service, handler) = Create();
        handler.Respond = _ => "{\"status\":false,\"message\":\"Invalid key\"}";

        var result = await service.InitiatePaymentAsync(new PaymentRequest { Amount = 1m, Email = "a@b.co", Reference = "R" });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Verify_ParsesTransaction()
    {
        var (service, handler) = Create();
        handler.Respond = _ => """
            {"status":true,"data":{"id":99,"status":"success","reference":"TNV-1","amount":350000,"currency":"NGN","channel":"card",
             "paid_at":"2026-11-07T10:00:00.000Z","customer":{"email":"guest@example.com"},"plan":null}}
            """;

        var result = await service.VerifyPaymentAsync("TNV-1");

        Assert.Equal("/transaction/verify/TNV-1", handler.Requests.Single().Path);
        Assert.True(result.Success);
        Assert.Equal(350000, result.AmountKobo);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal(99, result.TransactionId);
        Assert.Equal("guest@example.com", result.CustomerEmail);
        Assert.Equal(new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc), result.PaidAt);
    }

    [Fact]
    public async Task Verify_SuccessForADifferentReference_IsNotSuccess()
    {
        var (service, handler) = Create();
        handler.Respond = _ => "{\"status\":true,\"data\":{\"status\":\"success\",\"reference\":\"OTHER\",\"amount\":350000,\"currency\":\"NGN\"}}";

        Assert.False((await service.VerifyPaymentAsync("TNV-1")).Success);
    }

    [Theory]
    [InlineData("abandoned")]
    [InlineData("failed")]
    [InlineData("ongoing")]
    public async Task Verify_NonSuccessStatuses_AreNotSuccess(string status)
    {
        var (service, handler) = Create();
        handler.Respond = _ => $"{{\"status\":true,\"data\":{{\"status\":\"{status}\",\"reference\":\"TNV-1\",\"amount\":350000,\"currency\":\"NGN\"}}}}";

        var result = await service.VerifyPaymentAsync("TNV-1");

        Assert.False(result.Success);
        Assert.Equal(status, result.Status);
    }

    [Fact]
    public async Task Verify_TransportFailure_IsPendingNotSuccess()
    {
        var (service, handler) = Create();
        handler.Respond = _ => throw new HttpRequestException("network down");

        var result = await service.VerifyPaymentAsync("TNV-1");

        Assert.False(result.Success);
        Assert.Equal("pending", result.Status);
    }

    [Fact]
    public void WebhookSignature_IsHmacSha512OfRawBody()
    {
        var (service, _) = Create();
        var raw = Encoding.UTF8.GetBytes("{\"event\":\"charge.success\",\"data\":{\"reference\":\"TNV-1\"}}");
        var signature = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(TestKey), raw)).ToLowerInvariant();

        Assert.True(service.IsValidWebhookSignature(raw, signature));
        Assert.True(service.IsValidWebhookSignature(raw, signature.ToUpperInvariant()));
        Assert.False(service.IsValidWebhookSignature(Encoding.UTF8.GetBytes("{\"event\":\"charge.success\",\"data\":{\"reference\":\"TNV-2\"}}"), signature));
        Assert.False(service.IsValidWebhookSignature(raw, null));
        Assert.False(service.IsValidWebhookSignature(raw, "deadbeef"));
    }

    [Fact]
    public void WebhookSignature_RejectedWhenUnconfigured()
    {
        var (service, _) = Create(key: null);
        Assert.False(service.IsValidWebhookSignature(Encoding.UTF8.GetBytes("{}"), "anything"));
    }

    [Fact]
    public async Task Subscription_FetchDisableAndManageLink()
    {
        var (service, handler) = Create();
        handler.Respond = request => request.RequestUri!.AbsolutePath switch
        {
            "/subscription/SUB_1" => """
                {"status":true,"data":{"subscription_code":"SUB_1","status":"active","email_token":"tok","amount":750000,
                 "next_payment_date":"2026-12-07T00:00:00.000Z","customer":{"email":"biz@example.com","customer_code":"CUS_1"},"plan":{"plan_code":"PLN_1"}}}
                """,
            "/subscription/SUB_1/manage/link" => "{\"status\":true,\"data\":{\"link\":\"https://paystack.com/manage/abc\"}}",
            _ => "{\"status\":true,\"message\":\"Subscription disabled successfully\"}"
        };

        var subscription = await service.FetchSubscriptionAsync("SUB_1");
        var disabled = await service.DisableSubscriptionAsync("SUB_1", "tok");
        var link = await service.GetSubscriptionManageLinkAsync("SUB_1");

        Assert.Equal("active", subscription!.Status);
        Assert.Equal("tok", subscription.EmailToken);
        Assert.Equal("biz@example.com", subscription.CustomerEmail);
        Assert.Equal("PLN_1", subscription.PlanCode);
        Assert.Equal(new DateTime(2026, 12, 7, 0, 0, 0, DateTimeKind.Utc), subscription.NextPaymentDate);
        Assert.True(disabled);
        using var disableBody = JsonDocument.Parse(handler.Requests.Single(r => r.Path == "/subscription/disable").Body!);
        Assert.Equal("SUB_1", disableBody.RootElement.GetProperty("code").GetString());
        Assert.Equal("tok", disableBody.RootElement.GetProperty("token").GetString());
        Assert.Equal("https://paystack.com/manage/abc", link);
    }

    [Fact]
    public void Kobo_ConversionIsExact()
    {
        Assert.Equal(350_000, Money.ToKobo(3_500m));
        Assert.Equal(700_000, Money.ToKobo(7_000m));
        Assert.Equal(750_000, Money.ToKobo(7_500m));
        Assert.Equal(350_001, Money.ToKobo(3_500.005m));
        Assert.Equal(3_500m, Money.FromKobo(350_000));
    }
}
