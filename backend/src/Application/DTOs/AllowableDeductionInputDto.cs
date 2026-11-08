namespace TaxNavi.Application.DTOs;

public class AllowableDeductionInputDto
{
    public decimal PensionContributions { get; set; }
    public decimal NhfContributions { get; set; }
    public decimal NhisContributions { get; set; }
    public decimal LifeAssurancePremium { get; set; }
    public decimal MortgageInterest { get; set; }
    public decimal AnnualRentPaid { get; set; }
}
