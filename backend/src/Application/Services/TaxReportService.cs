using System.Text.Json;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Application.Services;

public class TaxReportService : ITaxReportService
{
    private readonly ITaxCalculationEngine _engine;
    private readonly ITaxProfileRepository _profiles;
    private readonly ITaxReportRepository _reports;
    private readonly IPaymentRepository _payments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public TaxReportService(
        ITaxCalculationEngine engine,
        ITaxProfileRepository profiles,
        ITaxReportRepository reports,
        IPaymentRepository payments,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _engine = engine;
        _profiles = profiles;
        _reports = reports;
        _payments = payments;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<CreatedReport> CreateAsync(TaxCalculationRequestDto request, Guid? userId, string? guestEmail = null)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var report = _engine.GenerateFullReport(request);

        var profile = new TaxProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProfileType = request.TaxpayerType,
            JsonData = JsonSerializer.Serialize(request),
            CreatedAt = now
        };
        string? token = null;
        var entity = new TaxReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TaxProfileId = profile.Id,
            ReportType = report.TaxpayerType,
            TaxYear = report.TaxYear,
            RuleSetId = report.RuleSet.Id,
            TotalIncome = report.GrossIncome,
            AllowableDeductions = report.TotalDeductionsAndReliefs,
            TaxLiability = report.TaxPayable,
            Summary = $"{report.RuleSet.Name} · tax year {report.TaxYear}",
            JsonData = JsonSerializer.Serialize(report),
            CreatedAt = now
        };

        if (userId == null)
        {
            token = ReportAccessTokens.Generate();
            entity.AccessTokenHash = ReportAccessTokens.Hash(token);
            entity.GuestEmail = guestEmail?.Trim().ToLowerInvariant();
        }

        // The calculation profile and its report are written together, or not at all.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _profiles.AddAsync(profile, ct);
            await _reports.AddAsync(entity, ct);
        });
        return new CreatedReport(entity, report, token);
    }

    public async Task DeleteAsync(TaxReport report, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Payments are financial history and are kept; they only lose the link to the deleted report.
            foreach (var payment in await _payments.GetByTaxReportIdAsync(report.Id, ct))
            {
                payment.TaxReportId = null;
                payment.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
                await _payments.UpdateAsync(payment, ct);
            }

            await _reports.DeleteAsync(report.Id, ct);

            // The calculation inputs were stored only for this report: delete them too instead of orphaning them.
            if (!await _reports.AnyForProfileAsync(report.TaxProfileId, ct))
            {
                await _profiles.DeleteAsync(report.TaxProfileId, ct);
            }
        }, cancellationToken);
    }
}
