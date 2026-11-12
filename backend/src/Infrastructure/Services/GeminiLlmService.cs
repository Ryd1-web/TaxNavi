using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Models;

namespace TaxNavi.Infrastructure.Services;

/// <summary>
/// Chat completions over the Gemini REST API. Plain HttpClient (the same approach as the
/// Paystack gateway) so the provider can be swapped by replacing this one class.
/// Logs never contain prompts, answers or the API key.
/// </summary>
public sealed class GeminiLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiLlmService> _logger;

    public GeminiLlmService(HttpClient http, GeminiOptions options, ILogger<GeminiLlmService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
        if (IsConfigured && !_http.DefaultRequestHeaders.Contains("x-goog-api-key"))
        {
            _http.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);
    public string ModelName => _options.ChatModel;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new LlmResponse { Success = false, Error = "AI is not configured.", Model = ModelName };
        }

        if (GeminiRetry.InCooldown)
        {
            return new LlmResponse { Success = false, Error = "quota", Model = ModelName };
        }

        object generationConfig = _options.ThinkingBudget is { } budget
            ? new { temperature = request.Temperature, maxOutputTokens = request.MaxOutputTokens, thinkingConfig = new { thinkingBudget = budget } }
            : new { temperature = request.Temperature, maxOutputTokens = request.MaxOutputTokens };

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = request.SystemInstruction } } },
            contents = request.Messages
                .Select(m => new { role = m.Role, parts = new[] { new { text = m.Text } } })
                .ToArray(),
            generationConfig
        };

        using var response = await GeminiRetry.SendAsync(
            () => _http.PostAsJsonAsync($"models/{Uri.EscapeDataString(_options.ChatModel)}:generateContent", body, cancellationToken),
            _options.MaxRetries,
            TimeSpan.FromSeconds(_options.MaxChatRetryWaitSeconds),
            TimeSpan.FromMinutes(_options.QuotaCooldownMinutes),
            _logger,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var category = response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "quota",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "auth",
                HttpStatusCode.NotFound => "model",
                _ => "provider"
            };
            _logger.LogError("Gemini generateContent failed: {Status} ({Category})", (int)response.StatusCode, category);
            return new LlmResponse { Success = false, Error = category, Model = ModelName };
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = json.RootElement;

        if (root.TryGetProperty("promptFeedback", out var feedback) &&
            feedback.TryGetProperty("blockReason", out var block))
        {
            _logger.LogWarning("Gemini blocked a prompt: {Reason}", block.GetString());
            return new LlmResponse { Success = false, Error = "blocked", Model = ModelName };
        }

        string text = string.Empty;
        string? finishReason = null;
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            finishReason = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;
            if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
            {
                text = string.Concat(parts.EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _))
                    .Select(p => p.GetProperty("text").GetString()));
            }
        }

        int promptTokens = 0, totalTokens = 0;
        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            promptTokens = usage.TryGetProperty("promptTokenCount", out var pt) ? pt.GetInt32() : 0;
            totalTokens = usage.TryGetProperty("totalTokenCount", out var tt) ? tt.GetInt32() : 0;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Gemini returned no text (finishReason: {Reason})", finishReason);
            return new LlmResponse { Success = false, Error = "empty", Model = ModelName, FinishReason = finishReason };
        }

        return new LlmResponse
        {
            Success = true,
            Text = text,
            Model = ModelName,
            InputTokens = promptTokens,
            // Output cost covers everything generated (thinking included), not just visible text.
            OutputTokens = Math.Max(0, totalTokens - promptTokens),
            FinishReason = finishReason
        };
    }
}
