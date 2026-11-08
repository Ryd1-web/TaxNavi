namespace TaxNavi.Domain.Models;

public class PaymentVerificationResponse
{
    /// <summary>True only when the provider confirms a successful charge for this exact reference.</summary>
    public bool Success { get; set; }

    /// <summary>Provider status: success, failed, abandoned, reversed, ongoing, pending, …</summary>
    public string Status { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public long AmountKobo { get; set; }
    public string Currency { get; set; } = string.Empty;
    public long? TransactionId { get; set; }
    public string? Channel { get; set; }
    public string? GatewayResponse { get; set; }
    public string? CustomerEmail { get; set; }
    public string? PlanCode { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Error { get; set; }
}
