namespace TaxNavi.Application.DTOs;

public class RuleSetSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string LegalBasis { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
    public List<TaxSourceDto> Sources { get; set; } = new();
}
