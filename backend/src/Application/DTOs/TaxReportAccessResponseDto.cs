namespace TaxNavi.Application.DTOs;

public class TaxReportAccessResponseDto
{
    public Guid? ReportId { get; set; }
    public bool AccessGranted { get; set; }
    public string AccessLevel { get; set; } = "Free";
    public string? UnlockedVia { get; set; }
    public FreeTaxSummaryDto FreeSummary { get; set; } = new();
    public FullTaxReportDto? FullReport { get; set; }
    public PremiumGateDto PremiumGate { get; set; } = new();
}
