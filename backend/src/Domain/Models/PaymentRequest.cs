namespace TaxNavi.Domain.Models;

public class PaymentRequest
{
    /// <summary>Amount in naira. Ignored by the provider when <see cref="PlanCode"/> is set.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Email { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string? CallbackUrl { get; set; }

    /// <summary>Where the customer lands if they cancel on the checkout page.</summary>
    public string? CancelUrl { get; set; }
    public string? PlanCode { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
