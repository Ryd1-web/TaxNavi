using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Entities;
using TaxNavi.Infrastructure.Services;
using Xunit;

namespace TaxNavi.Application.Tests;

public class PdfReportServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendersPremiumReportFromSnapshot(bool company)
    {
        var request = company
            ? new TaxCalculationRequestDto
            {
                TaxYear = 2026,
                TaxpayerType = TaxpayerKinds.SME,
                BusinessStructure = BusinessStructures.LimitedCompany,
                IncomeSources = new() { new() { Name = "Sales", Category = IncomeCategories.Business, AnnualAmount = 300_000_000m } },
                Expenses = new() { new() { Name = "Costs", Category = ExpenseCategories.Operating, AnnualAmount = 120_000_000m } },
                Vat = new() { AnnualTurnover = 300_000_000m }
            }
            : TestRules.Salary(5_000_000m, 2026);

        var report = TestRules.Engine.GenerateFullReport(request);
        var entity = new TaxReport { Id = Guid.NewGuid(), TaxYear = 2026, JsonData = JsonSerializer.Serialize(report) };

        var bytes = await new PdfReportService(NullLogger<PdfReportService>.Instance)
            .GenerateTaxReportPdfAsync(entity, "summary", string.Empty, "Ada Obi");

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
