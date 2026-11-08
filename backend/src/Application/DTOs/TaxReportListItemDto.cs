namespace TaxNavi.Application.DTOs;

public class TaxReportListItemDto
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public int TaxYear { get; set; }
    public string RuleSetId { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal AllowableDeductions { get; set; }
    public decimal TaxLiability { get; set; }
    public bool IsUnlocked { get; set; }
    public DateTime CreatedAt { get; set; }
}
