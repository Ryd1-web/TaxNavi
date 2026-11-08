namespace TaxNavi.Application.DTOs;

public class IncomeStreamDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = IncomeCategories.Other;
    public decimal AnnualAmount { get; set; }

    /// <summary>True when withholding tax already deducted on this income is a final tax.</summary>
    public bool FinalTaxDeducted { get; set; }
}
