namespace TaxNavi.Domain.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Plan { get; set; } = "SmeMonthly";
    public string Status { get; set; } = SubscriptionStatuses.Active;

    /// <summary>Billing amount per period in naira, from the verified payment or Paystack's subscription.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public Guid? LastPaymentId { get; set; }

    /// <summary>True for Paystack-managed recurring subscriptions; false for one-off monthly passes.</summary>
    public bool IsRecurring { get; set; }
    public string? PlanCode { get; set; }
    public string? ProviderSubscriptionCode { get; set; }

    /// <summary>Paystack email token, required to disable the subscription. Never logged or returned to clients.</summary>
    public string? ProviderEmailToken { get; set; }
    public string? ProviderCustomerCode { get; set; }
    public DateTime? NextPaymentDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? LastPaymentFailedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }

    /// <summary>Server-side entitlement rule for the SME plan.</summary>
    public bool IsActiveAt(DateTime utcNow, int pastDueGraceDays = 3) => Status switch
    {
        SubscriptionStatuses.Active or SubscriptionStatuses.NonRenewing or SubscriptionStatuses.Cancelled => CurrentPeriodEnd > utcNow,
        SubscriptionStatuses.PastDue => CurrentPeriodEnd.AddDays(pastDueGraceDays) > utcNow,
        _ => false
    };
}
