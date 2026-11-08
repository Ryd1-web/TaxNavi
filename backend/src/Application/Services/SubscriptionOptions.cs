namespace TaxNavi.Application.Services;

public class SubscriptionOptions
{
    /// <summary>Paystack plan code (PLN_…) for the SME monthly plan. Empty = one-off monthly passes.</summary>
    public string? SmePlanCode { get; set; }
}
