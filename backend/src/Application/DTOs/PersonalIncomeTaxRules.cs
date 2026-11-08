namespace TaxNavi.Application.DTOs;

public class PersonalIncomeTaxRules
{
    public string RegimeName { get; set; } = string.Empty;
    public string ReliefModel { get; set; } = ReliefModels.RentRelief;
    public decimal FlatCraAmount { get; set; }
    public decimal CraFloorRate { get; set; }
    public decimal CraRate { get; set; }
    public decimal RentReliefRate { get; set; }
    public decimal RentReliefCap { get; set; }
    public List<TaxBandRule> Bands { get; set; } = new();
}
