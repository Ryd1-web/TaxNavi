using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TaxNavi.Infrastructure.Services;

/// <summary>
/// Free-tier friendly HTTP handling for Gemini: retries 429/5xx with the server's suggested delay
/// (or exponential backoff), and opens a shared cooldown after a quota rejection so the app stops
/// hammering an exhausted quota — callers fall back to the rules assistant until it closes.
/// </summary>
public static partial class GeminiRetry
{
    private static long _cooldownUntilTicks;

    public static bool InCooldown => DateTime.UtcNow.Ticks < Interlocked.Read(ref _cooldownUntilTicks);

    public static void StartCooldown(TimeSpan duration)
    {
        var until = DateTime.UtcNow.Add(duration).Ticks;
        long current;
        do
        {
            current = Interlocked.Read(ref _cooldownUntilTicks);
            if (until <= current) return;
        }
        while (Interlocked.CompareExchange(ref _cooldownUntilTicks, until, current) != current);
    }

    /// <summary>Test hook.</summary>
    public static void ResetCooldown() => Interlocked.Exchange(ref _cooldownUntilTicks, 0);

    [GeneratedRegex(@"""retryDelay""\s*:\s*""(\d+(?:\.\d+)?)s""")]
    private static partial Regex RetryDelay();

    public static async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        int maxAttempts,
        TimeSpan maxWait,
        TimeSpan cooldownAfterQuota,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await send();
            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            if (!retryable)
            {
                return response;
            }

            var delay = await SuggestedDelayAsync(response, attempt, cancellationToken);
            if (attempt >= maxAttempts || delay > maxWait)
            {
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // Long waits mean a per-day/per-minute quota is spent: back off app-wide.
                    StartCooldown(delay > cooldownAfterQuota ? delay : cooldownAfterQuota);
                    logger.LogWarning("Gemini quota exhausted; AI calls paused for {Minutes:F0} min", (delay > cooldownAfterQuota ? delay : cooldownAfterQuota).TotalMinutes);
                }

                return response;
            }

            logger.LogInformation("Gemini returned {Status}; retrying in {Delay:F1}s (attempt {Attempt}/{Max})", (int)response.StatusCode, delay.TotalSeconds, attempt, maxAttempts);
            response.Dispose();
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static async Task<TimeSpan> SuggestedDelayAsync(HttpResponseMessage response, int attempt, CancellationToken cancellationToken)
    {
        if (response.Headers.RetryAfter?.Delta is { } header)
        {
            return header;
        }

        try
        {
            // Google's RetryInfo: "retryDelay": "37s" (body is buffered so callers can still read it).
            await response.Content.LoadIntoBufferAsync();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var match = RetryDelay().Match(body);
            if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                return TimeSpan.FromSeconds(Math.Ceiling(seconds) + 1);
            }
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidOperationException)
        {
            // fall through to backoff
        }

        return TimeSpan.FromSeconds(Math.Min(60, 2 * Math.Pow(2, attempt - 1)));
    }
}
