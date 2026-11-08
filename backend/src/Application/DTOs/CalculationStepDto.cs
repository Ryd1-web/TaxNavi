namespace TaxNavi.Application.DTOs;

public class CalculationStepDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
