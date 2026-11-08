using System.Globalization;

namespace TaxNavi.Application.Services;

/// <summary>Single formatting culture for user-facing figures: "1,234,567" and "15%" regardless of server locale.</summary>
public static class TaxFormat
{
    public static readonly CultureInfo Culture = CreateCulture();

    private static CultureInfo CreateCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PercentPositivePattern = 1; // n%
        culture.NumberFormat.PercentNegativePattern = 1; // -n%
        return CultureInfo.ReadOnly(culture);
    }
}
