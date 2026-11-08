namespace TaxNavi.Infrastructure.Services;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

/// <summary>
/// Paystack gateway: Transactions (initialize, verify), Subscriptions (fetch, disable, manage link) and webhook
/// signature validation. Amounts are sent and compared in kobo. Nothing here ever reports success without
/// Paystack's confirmation.
/// </summary>
public class PaystackPaymentService : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };

    private readonly HttpClient _http;
    private readonly ILogger<PaystackPaymentService> _logger;
    private readonly string? _secretKey;

    public PaystackPaymentService(HttpClient http, PaystackOptions options, ILogger<PaystackPaymentService> logger)
    {
        _http = http;
        _logger = logger;

        var key = options.SecretKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Contains("your", StringComparison.OrdinalIgnoreCase))
        {
            _secretKey = null;
        }
        else if (key.StartsWith("sk_test_", StringComparison.Ordinal))
        {
            _secretKey = key;
            IsTestMode = true;
        }
        else if (key.StartsWith("sk_live_", StringComparison.Ordinal) && options.AllowLiveKeys)
        {
            _secretKey = key;
        }
        else
        {
            _secretKey = null;
            _logger.LogError(key.StartsWith("sk_live_", StringComparison.Ordinal)
                ? "A live Paystack key is configured but Paystack:AllowLiveKeys is false. Payments are disabled."
                : "The configured Paystack secret key is not a valid sk_test_/sk_live_ key. Payments are disabled.");
        }

        _http.BaseAddress ??= new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
        if (_secretKey != null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        }
    }

    public bool IsConfigured => _secretKey != null;
    public bool IsTestMode { get; }

    public async Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new PaymentInitiationResponse { Success = false, Error = "Payments are not configured." };
        }

        var metadata = new Dictionary<string, object?>();
        foreach (var (key, value) in request.Metadata)
        {
            metadata[key] = value;
        }
        if (!string.IsNullOrWhiteSpace(request.CancelUrl))
        {
            // Paystack redirects here when the customer clicks "Cancel" on the checkout page.
            metadata["cancel_action"] = request.CancelUrl;
        }

        var body = new Dictionary<string, object?>
        {
            ["email"] = request.Email,
            ["amount"] = Money.ToKobo(request.Amount).ToString(CultureInfo.InvariantCulture),
            ["currency"] = request.Currency,
            ["reference"] = request.Reference,
            ["metadata"] = metadata
        };
        if (!string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            body["callback_url"] = request.CallbackUrl;
        }
        if (!string.IsNullOrWhiteSpace(request.PlanCode))
        {
            // With a plan, Paystack charges the plan amount and creates the subscription after payment.
            body["plan"] = request.PlanCode;
        }

        var result = await SendAsync(HttpMethod.Post, "transaction/initialize", body, cancellationToken);
        if (result?.Data is not { ValueKind: JsonValueKind.Object } data)
        {
            _logger.LogWarning("Paystack initialize rejected reference {Reference}: {Message}", request.Reference, result?.Message);
            return new PaymentInitiationResponse { Success = false, Error = result?.Message ?? "Payment provider rejected the request." };
        }

        return new PaymentInitiationResponse
        {
            Success = true,
            Reference = Str(data, "reference") ?? request.Reference,
            AuthorizationUrl = Str(data, "authorization_url") ?? string.Empty,
            AccessCode = Str(data, "access_code") ?? string.Empty
        };
    }

    public async Task<PaymentVerificationResponse> VerifyPaymentAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new PaymentVerificationResponse { Success = false, Status = "unconfigured", Reference = reference };
        }

        var result = await SendAsync(HttpMethod.Get, $"transaction/verify/{Uri.EscapeDataString(reference)}", null, cancellationToken);
        if (result?.Data is not { ValueKind: JsonValueKind.Object } data)
        {
            // Unknown or not-yet-available transactions are treated as pending, never as success.
            return new PaymentVerificationResponse { Success = false, Status = "pending", Reference = reference, Error = result?.Message };
        }

        var status = Str(data, "status") ?? "unknown";
        var returnedReference = Str(data, "reference") ?? string.Empty;
        var plan = Prop(data, "plan");
        var planCode = plan.ValueKind == JsonValueKind.String ? plan.GetString() : Str(plan, "plan_code") ?? Str(Prop(data, "plan_object"), "plan_code");

        return new PaymentVerificationResponse
        {
            Success = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase) &&
                      string.Equals(returnedReference, reference, StringComparison.Ordinal),
            Status = status,
            Reference = returnedReference,
            AmountKobo = Long(data, "amount") ?? 0,
            Currency = Str(data, "currency") ?? string.Empty,
            TransactionId = Long(data, "id"),
            Channel = Str(data, "channel"),
            GatewayResponse = Str(data, "gateway_response"),
            CustomerEmail = Str(Prop(data, "customer"), "email"),
            PlanCode = string.IsNullOrWhiteSpace(planCode) ? null : planCode,
            PaidAt = Date(data, "paid_at") ?? Date(data, "paidAt")
        };
    }

    public bool IsValidWebhookSignature(byte[] rawBody, string? signature)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        // HMAC-SHA512 of the exact raw request bytes, keyed with the secret key, hex encoded.
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_secretKey!));
        var expected = Encoding.ASCII.GetBytes(Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant());
        var provided = Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    public async Task<ProviderSubscription?> FetchSubscriptionAsync(string subscriptionCode, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var result = await SendAsync(HttpMethod.Get, $"subscription/{Uri.EscapeDataString(subscriptionCode)}", null, cancellationToken);
        if (result?.Data is not { ValueKind: JsonValueKind.Object } data)
        {
            return null;
        }

        var customer = Prop(data, "customer");
        return new ProviderSubscription
        {
            SubscriptionCode = Str(data, "subscription_code") ?? subscriptionCode,
            Status = Str(data, "status") ?? string.Empty,
            EmailToken = Str(data, "email_token"),
            CustomerEmail = Str(customer, "email"),
            CustomerCode = Str(customer, "customer_code"),
            PlanCode = Str(Prop(data, "plan"), "plan_code"),
            AmountKobo = Long(data, "amount") ?? 0,
            NextPaymentDate = Date(data, "next_payment_date"),
            CreatedAt = Date(data, "createdAt") ?? Date(data, "created_at")
        };
    }

    public async Task<bool> DisableSubscriptionAsync(string subscriptionCode, string emailToken, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var result = await SendAsync(HttpMethod.Post, "subscription/disable",
            new Dictionary<string, object?> { ["code"] = subscriptionCode, ["token"] = emailToken }, cancellationToken);
        return result?.Status == true;
    }

    public async Task<string?> GetSubscriptionManageLinkAsync(string subscriptionCode, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var result = await SendAsync(HttpMethod.Get, $"subscription/{Uri.EscapeDataString(subscriptionCode)}/manage/link", null, cancellationToken);
        return result?.Data is { ValueKind: JsonValueKind.Object } data ? Str(data, "link") : null;
    }

    // -----------------------------------------------------------------------

    private sealed record Envelope(bool Status, string? Message, JsonElement? Data);

    private async Task<Envelope?> SendAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }

            using var response = await _http.SendAsync(request, cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var ok = root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.True && response.IsSuccessStatusCode;
            var message = Str(root, "message");
            JsonElement? data = root.TryGetProperty("data", out var d) ? d.Clone() : null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack {Method} {Path} returned HTTP {Status}", method, RedactPath(path), (int)response.StatusCode);
            }
            return new Envelope(ok, message, ok ? data : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Paystack {Method} {Path} failed", method, RedactPath(path));
            return null;
        }
    }

    // Subscription codes and references are not secrets, but keep logs tidy and short.
    private static string RedactPath(string path) => path.Length > 60 ? path[..60] : path;

    private static JsonElement Prop(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string? Str(JsonElement element, string name)
    {
        var value = Prop(element, name);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static long? Long(JsonElement element, string name)
    {
        var value = Prop(element, name);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private static DateTime? Date(JsonElement element, string name) =>
        DateTime.TryParse(Str(element, name), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}
