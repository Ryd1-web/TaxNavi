using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Infrastructure.Services;

namespace TaxNavi.Api.Tests;

/// <summary>
/// Boots the real API in-process with an in-memory database and a stateful fake Paystack API.
/// No request ever leaves the process; the user's real Paystack keys are never used.
/// </summary>
public sealed class PaystackTestFactory : WebApplicationFactory<Program>
{
    public const string SecretKey = "sk_test_integration_000000000000";

    public FakePaystackApi Paystack { get; } = new();

    static PaystackTestFactory()
    {
        // Environment variables are read during Program's builder phase and override user secrets.
        Environment.SetEnvironmentVariable("UseInMemoryDatabase", "true");
        Environment.SetEnvironmentVariable("DatabaseSetup__ApplyOnStartup", "true");
        Environment.SetEnvironmentVariable("Paystack__SecretKey", SecretKey);
        Environment.SetEnvironmentVariable("Paystack__SmePlanCode", "");
        Environment.SetEnvironmentVariable("Paystack__BaseUrl", "https://paystack.stub/");
        Environment.SetEnvironmentVariable("RateLimits__CheckoutPerMinute", "10000");
        Environment.SetEnvironmentVariable("RateLimits__CalculatePerMinute", "10000");
        Environment.SetEnvironmentVariable("RateLimits__AuthPerMinute", "10000");
        Environment.SetEnvironmentVariable("App__FrontendUrl", "https://app.test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<IPaymentService, PaystackPaymentService>()
                .ConfigurePrimaryHttpMessageHandler(() => Paystack);
        });
    }

    public static string Sign(string payload) =>
        Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(SecretKey), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    public async Task<HttpResponseMessage> SendWebhookAsync(HttpClient client, string payload, string? signature = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook/paystack")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-paystack-signature", signature ?? Sign(payload));
        return await client.SendAsync(request);
    }
}

/// <summary>Minimal stateful stand-in for api.paystack.co (initialize + verify).</summary>
public sealed class FakePaystackApi : HttpMessageHandler
{
    public sealed class Transaction
    {
        public string Reference { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public long AmountKobo { get; set; }
        public string Currency { get; set; } = "NGN";
        public string Status { get; set; } = "ongoing";
        public string? CancelAction { get; init; }
    }

    public ConcurrentDictionary<string, Transaction> Transactions { get; } = new();
    public ConcurrentBag<string> AuthorizationHeaders { get; } = new();

    public Transaction Pay(string reference, long? amountKobo = null)
    {
        var tx = Transactions[reference];
        tx.Status = "success";
        if (amountKobo.HasValue) tx.AmountKobo = amountKobo.Value;
        return tx;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AuthorizationHeaders.Add(request.Headers.Authorization?.ToString() ?? string.Empty);
        var path = request.RequestUri!.AbsolutePath;

        if (request.Method == HttpMethod.Post && path == "/transaction/initialize")
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var root = body.RootElement;
            var reference = root.GetProperty("reference").GetString()!;
            var amount = root.GetProperty("amount");
            Transactions[reference] = new Transaction
            {
                Reference = reference,
                Email = root.GetProperty("email").GetString()!,
                AmountKobo = amount.ValueKind == JsonValueKind.String ? long.Parse(amount.GetString()!) : amount.GetInt64(),
                Currency = root.GetProperty("currency").GetString()!,
                CancelAction = root.GetProperty("metadata").TryGetProperty("cancel_action", out var c) ? c.GetString() : null
            };
            return Json(new { status = true, message = "Authorization URL created", data = new { authorization_url = $"https://checkout.paystack.test/{reference}", access_code = "ac", reference } });
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/transaction/verify/"))
        {
            var reference = Uri.UnescapeDataString(path["/transaction/verify/".Length..]);
            if (!Transactions.TryGetValue(reference, out var tx))
            {
                return Json(new { status = false, message = "Transaction reference not found" }, HttpStatusCode.BadRequest);
            }
            return Json(new
            {
                status = true,
                message = "Verification successful",
                data = new
                {
                    id = Math.Abs(reference.GetHashCode()),
                    status = tx.Status,
                    reference = tx.Reference,
                    amount = tx.AmountKobo,
                    currency = tx.Currency,
                    channel = "card",
                    paid_at = tx.Status == "success" ? DateTime.UtcNow.ToString("O") : null,
                    customer = new { email = tx.Email }
                }
            });
        }

        return Json(new { status = false, message = "Not implemented in fake" }, HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(object body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = JsonContent.Create(body) };
}

internal static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text).RootElement.Clone();
    }

    public static HttpRequestMessage WithToken(this HttpRequestMessage request, string? accessToken = null, string? bearer = null)
    {
        if (accessToken != null) request.Headers.Add("X-Report-Access-Token", accessToken);
        if (bearer != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }
}
