namespace TaxNavi.Application.DTOs;

public class TaxCalculationRequestDto
{
    /// <summary>Tax (calendar) year being assessed. Defaults to the configured current year.</summary>
    public int? TaxYear { get; set; }
    public string TaxpayerType { get; set; } = TaxpayerKinds.Individual;
    public string BusinessType { get; set; } = string.Empty;
    public string BusinessStructure { get; set; } = string.Empty;
    public decimal FixedAssets { get; set; }
    public bool IsProfessionalServices { get; set; }
    public List<IncomeStreamDto> IncomeSources { get; set; } = new();
    public List<ExpenseItemDto> Expenses { get; set; } = new();
    public AllowableDeductionInputDto AllowableDeductions { get; set; } = new();
    public VatCheckInputDto Vat { get; set; } = new();
}
