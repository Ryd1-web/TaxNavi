namespace TaxNavi.Application.Services;

public class UnsupportedTaxYearException : Exception
{
    public UnsupportedTaxYearException(int taxYear, IReadOnlyList<int> supported)
        : base($"Tax year {taxYear} is not supported. Supported tax years: {string.Join(", ", supported)}.")
    {
    }
}
