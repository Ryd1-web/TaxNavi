namespace TaxNavi.Domain.Models;

public class ProviderSubscription
{
    public string SubscriptionCode { get; set; } = string.Empty;

    /// <summary>active, non-renewing, attention, completed, cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    public string? EmailToken { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerCode { get; set; }
    public string? PlanCode { get; set; }
    public long AmountKobo { get; set; }
    public DateTime? NextPaymentDate { get; set; }
    public DateTime? CreatedAt { get; set; }
}
