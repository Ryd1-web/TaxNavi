using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

public interface ITaxAccessControlService
{
    FreeTaxSummaryDto GetFreeTaxSummary(FullTaxReportDto report);

    /// <summary>
    /// Builds the response for a report. <paramref name="unlockedVia"/> must come from a
    /// server-side entitlement check; pass null to return the free tier only.
    /// </summary>
    TaxReportAccessResponseDto BuildResponse(FullTaxReportDto report, string? unlockedVia, bool paymentsEnabled, Guid? reportId = null);
}
