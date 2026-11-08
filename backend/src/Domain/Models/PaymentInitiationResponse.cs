namespace TaxNavi.Domain.Models;

public class PaymentInitiationResponse
{
    public bool Success { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string AccessCode { get; set; } = string.Empty;
    public string? Error { get; set; }
}
