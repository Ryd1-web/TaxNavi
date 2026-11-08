using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Application.Services;

public interface ITaxReportService
{
    /// <summary>Computes and stores a report for a signed-in user, or for a guest (returns an access token).</summary>
    Task<CreatedReport> CreateAsync(TaxCalculationRequestDto request, Guid? userId, string? guestEmail = null);

    /// <summary>Deletes a report and its calculation profile; its payments are kept (unlinked) as history.</summary>
    Task DeleteAsync(TaxReport report, CancellationToken cancellationToken = default);
}
