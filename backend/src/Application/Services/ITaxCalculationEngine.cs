using TaxNavi.Application.DTOs;

namespace TaxNavi.Application.Services;

public interface ITaxCalculationEngine
{
    FullTaxReportDto GenerateFullReport(TaxCalculationRequestDto request);
}
