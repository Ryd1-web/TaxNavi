namespace TaxNavi.Application.DTOs;

public class MonetizationRules
{
    /// <summary>Fallback one-off report price when a report type has no entry in <see cref="ReportPrices"/>.</summary>
    public decimal PayPerReportPrice { get; set; }

    /// <summary>One-off premium report price per report type (Individual, SelfEmployed, SME), in naira.</summary>
    public Dictionary<string, decimal> ReportPrices { get; set; } = new();
    public decimal SmeSubscriptionPrice { get; set; }
    public int SmeSubscriptionPeriodDays { get; set; } = 30;

    /// <summary>Days of continued access after a failed renewal before the SME plan lapses.</summary>
    public int SubscriptionGraceDays { get; set; } = 3;

    /// <summary>An unpaid checkout is reused for this long instead of creating a duplicate transaction.</summary>
    public int PendingCheckoutMinutes { get; set; } = 30;
    public string Currency { get; set; } = "NGN";

    public decimal PriceFor(string reportType) =>
        ReportPrices.TryGetValue(reportType, out var price) && price > 0 ? price : PayPerReportPrice;
}
