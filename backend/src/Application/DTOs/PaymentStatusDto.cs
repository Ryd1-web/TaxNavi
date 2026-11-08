namespace TaxNavi.Application.DTOs;

public class PaymentStatusDto
{
    public string Reference { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public Guid? TaxReportId { get; set; }
    public bool IsGuest { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
