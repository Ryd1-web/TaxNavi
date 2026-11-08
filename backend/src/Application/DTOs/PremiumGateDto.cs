namespace TaxNavi.Application.DTOs;

public class PremiumGateDto
{
    public string Headline { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal PaymentAmount { get; set; }
    public decimal SubscriptionAmount { get; set; }
    public string Currency { get; set; } = "NGN";
    public List<string> LockedFeatures { get; set; } = new();
    public bool RequiresAccount { get; set; } = true;
    public bool PaymentsEnabled { get; set; }
}

// ---------------------------------------------------------------------------
// Rule configuration (bound from appsettings "TaxRules")
// ---------------------------------------------------------------------------
