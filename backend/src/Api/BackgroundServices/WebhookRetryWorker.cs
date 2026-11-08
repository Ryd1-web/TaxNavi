using TaxNavi.Application.Services;

namespace TaxNavi.Api.BackgroundServices;

/// <summary>
/// Re-processes stored Paystack webhook events that failed or never finished (e.g. the process stopped mid-way),
/// using the signature-verified payload saved on receipt. Processing is idempotent, so retries are safe.
/// </summary>
public class WebhookRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WebhookRetryWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _retryAfter;
    private readonly int _maxAttempts;
    private readonly int _batchSize;

    public WebhookRetryWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<WebhookRetryWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(10, configuration.GetValue("Webhooks:RetryIntervalSeconds", 60)));
        _retryAfter = TimeSpan.FromSeconds(Math.Max(10, configuration.GetValue("Webhooks:RetryAfterSeconds", 120)));
        _maxAttempts = Math.Max(1, configuration.GetValue("Webhooks:MaxAttempts", 8));
        _batchSize = Math.Max(1, configuration.GetValue("Webhooks:RetryBatchSize", 25));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var billing = scope.ServiceProvider.GetRequiredService<IBillingService>();
                var recovered = await billing.RetryPendingWebhooksAsync(_maxAttempts, _retryAfter, _batchSize, stoppingToken);
                if (recovered > 0)
                {
                    _logger.LogInformation("Recovered {Count} webhook event(s) on retry", recovered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Logged, never swallowed silently; the next tick tries again.
                _logger.LogError(ex, "Webhook retry pass failed");
            }
        }
    }
}
