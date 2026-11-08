namespace TaxNavi.Application.DTOs;

public class DeductionLineDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Claimed { get; set; }
    public decimal Allowed { get; set; }
    public string Note { get; set; } = string.Empty;
}
