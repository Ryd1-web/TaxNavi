namespace TaxNavi.Domain.Entities;

public static class WebhookEventStatuses
{
    /// <summary>Stored, not yet processed.</summary>
    public const string Received = "Received";

    /// <summary>An attempt is in progress (or crashed mid-way; retried once stale).</summary>
    public const string Processing = "Processing";
    public const string Processed = "Processed";

    /// <summary>The last attempt threw; retried by the background worker until the attempt limit.</summary>
    public const string Failed = "Failed";
}
