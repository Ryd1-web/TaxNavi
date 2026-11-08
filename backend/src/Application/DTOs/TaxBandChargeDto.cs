namespace TaxNavi.Application.DTOs;

public class TaxBandChargeDto
{
    public string Label { get; set; } = string.Empty;
    public decimal LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxAmount { get; set; }
}
