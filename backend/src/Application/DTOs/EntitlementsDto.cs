namespace TaxNavi.Application.DTOs;

public class EntitlementsDto
{
    public bool PaymentsEnabled { get; set; }
    public bool TestMode { get; set; }

    /// <summary>"Recurring" when a Paystack plan is configured, otherwise "OneTime" monthly passes.</summary>
    public string SubscriptionMode { get; set; } = "OneTime";
    public bool HasActiveSubscription { get; set; }
    public string? SubscriptionPlan { get; set; }
    public string? SubscriptionStatus { get; set; }
    public bool SubscriptionRecurring { get; set; }
    public DateTime? SubscriptionPeriodEnd { get; set; }
    public DateTime? NextPaymentDate { get; set; }
    public bool CanCancelSubscription { get; set; }
    public int UnlockedReportCount { get; set; }
    public MonetizationRules Pricing { get; set; } = new();
}
