namespace TaxNavi.Domain.Entities;

/// <summary>
/// Every signed Paystack webhook delivery, stored on receipt before processing. The unique <see cref="EventKey"/>
/// makes processing idempotent; <see cref="Payload"/> lets failed events be retried without Paystack re-sending.
/// </summary>
public class PaymentWebhookEvent
{
    public Guid Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    /// <summary>Transaction reference or subscription code the event is about.</summary>
    public string? Reference { get; set; }

    /// <summary>The signature-verified raw JSON body. Contains the customer's email; see retention notes in PAYMENTS.md.</summary>
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = WebhookEventStatuses.Received;
    public int Attempts { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>Set only after all business changes for the event have been committed.</summary>
    public DateTime? ProcessedAt { get; set; }
    public string? Outcome { get; set; }

    /// <summary>Exception type and message of the last failed attempt (no secrets or payload data).</summary>
    public string? LastError { get; set; }
}
