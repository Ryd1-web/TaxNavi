namespace TaxNavi.Application.DTOs;

public static class BusinessStructures
{
    public const string SoleProprietor = "SoleProprietor";
    public const string LimitedCompany = "LimitedCompany";

    public static bool IsSupported(string? value)
    {
        return string.IsNullOrEmpty(value) || value is SoleProprietor or LimitedCompany;
    }
}
