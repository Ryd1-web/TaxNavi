namespace TaxNavi.Application.DTOs;

public static class TaxpayerKinds
{
    public const string Individual = "Individual";
    public const string SelfEmployed = "SelfEmployed";
    public const string SME = "SME";

    public static bool IsSupported(string? value)
    {
        return value is Individual or SelfEmployed or SME;
    }
}
