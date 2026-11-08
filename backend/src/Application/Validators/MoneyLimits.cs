using FluentValidation;

namespace TaxNavi.Application.Validators;

public static class MoneyLimits
{
    /// <summary>₦1 trillion. Guards against overflow and obviously mistyped figures.</summary>
    public const decimal Max = 1_000_000_000_000m;
}
