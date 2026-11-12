using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

/// <summary>"Explain My Tax Results": plain-English explanation of an engine-computed report.</summary>
public interface IReportExplanationService
{
    Task<AiResult<ExplainReportResponseDto>> ExplainAsync(Guid? userId, string? clientKey, FullTaxReportDto report, CancellationToken cancellationToken = default);
}
