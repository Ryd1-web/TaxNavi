using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;

namespace TaxNavi.Application.Services;

public record CreatedReport(TaxReport Entity, FullTaxReportDto Report, string? AccessToken);
