namespace TaxNavi.Application.DTOs;

public class CompanyIncomeTaxRules
{
    public decimal SmallCompanyTurnoverLimit { get; set; }
    public decimal? SmallCompanyFixedAssetLimit { get; set; }
    public bool SmallCompanyExcludesProfessionalServices { get; set; }
    public decimal SmallCompanyRate { get; set; }
    public decimal? MediumCompanyTurnoverLimit { get; set; }
    public decimal? MediumCompanyRate { get; set; }
    public decimal StandardRate { get; set; }
    public string LevyName { get; set; } = string.Empty;
    public decimal LevyRate { get; set; }
    public bool LevyAppliesToSmallCompanies { get; set; }
    public decimal? LargeCompanyAdviceTurnover { get; set; }
    public string VerificationStatus { get; set; } = VerificationStatuses.PendingValidation;
}
