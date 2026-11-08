namespace TaxNavi.Domain.Interfaces;

using Entities;

public interface IPdfService
{
    Task<byte[]> GenerateTaxReportPdfAsync(TaxReport report, string summary, string advice, string userName = "Taxpayer");
}
