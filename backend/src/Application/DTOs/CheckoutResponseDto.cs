namespace TaxNavi.Application.DTOs;

public class CheckoutResponseDto
{
    public string Reference { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public Guid? TaxReportId { get; set; }

    /// <summary>Returned once, when a guest report is created. The client must keep it to open the report.</summary>
    public string? AccessToken { get; set; }

    /// <summary>True when an existing unpaid checkout was returned instead of creating a new transaction.</summary>
    public bool Reused { get; set; }
}
