namespace TaxNavi.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }

    /// <summary>Null for guest checkouts.</summary>
    public Guid? UserId { get; set; }
    public Guid? TaxReportId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public string Purpose { get; set; } = PaymentPurposes.PremiumReport;

    /// <summary>Amount in naira as charged; compared in kobo against the provider.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Status { get; set; } = PaymentStatuses.Pending;

    /// <summary>Our unique transaction reference, sent to Paystack.</summary>
    public string Reference { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = "Paystack";
    public string? PlanCode { get; set; }
    public string? AuthorizationUrl { get; set; }
    public long? ProviderTransactionId { get; set; }
    public string? Channel { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Set exactly once, atomically, when the purchase is delivered. Guards against double fulfilment.</summary>
    public DateTime? FulfilledAt { get; set; }

    /// <summary>
    /// Optimistic-concurrency token (PostgreSQL's xmin): a stale write — e.g. "abandoned" racing "success" — fails
    /// instead of overwriting.
    /// </summary>
    public uint Version { get; set; }

    // Navigation properties
    public User? User { get; set; }
}
