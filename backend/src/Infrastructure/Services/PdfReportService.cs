namespace TaxNavi.Infrastructure.Services;

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxNavi.Application.DTOs;
using TaxNavi.Application.Services;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;

/// <summary>
/// Renders premium tax reports on demand from the stored report snapshot. PDFs are not
/// persisted to disk, so no copy of a user's financial data is left in file storage.
/// </summary>
public class PdfReportService : IPdfService
{
    private const string Ink = "#0F172A";
    private const string Muted = "#64748B";
    private const string Line = "#E2E8F0";
    private const string Brand = "#0B6B4F";
    private static readonly CultureInfo Inv = TaxFormat.Culture;

    private readonly ILogger<PdfReportService> _logger;

    public PdfReportService(ILogger<PdfReportService> logger)
    {
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerateTaxReportPdfAsync(TaxReport report, string summary, string advice, string userName = "Taxpayer")
    {
        var detailed = TryDeserialize(report.JsonData);

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));

                page.Header().PaddingBottom(16).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("TaxNavi").FontSize(18).Bold().FontColor(Brand);
                        col.Item().Text("Personal & SME Tax Report").FontSize(10).FontColor(Muted);
                    });
                    row.ConstantItem(200).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text(userName).Bold();
                        col.Item().AlignRight().Text($"Generated {DateTime.UtcNow.ToString("d MMM yyyy", Inv)}").FontColor(Muted).FontSize(9);
                        col.Item().AlignRight().Text($"Ref {report.Id.ToString()[..8].ToUpperInvariant()}").FontColor(Muted).FontSize(9);
                    });
                });

                page.Content().Column(column =>
                {
                    column.Spacing(14);
                    if (detailed != null)
                    {
                        RenderDetailed(column, detailed);
                    }
                    else
                    {
                        RenderBasic(column, report, summary, advice);
                    }

                    column.Item().PaddingTop(8).BorderTop(1).BorderColor(Line).PaddingTop(8)
                        .Text("This report is an estimate based on the information provided and the rule set shown. It is not legal or tax advice. Confirm your position with the Nigeria Revenue Service, your State Internal Revenue Service or a qualified tax professional before filing.")
                        .FontSize(8).FontColor(Muted);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(t => t.FontSize(8).FontColor(Muted));
                    text.Span("TaxNavi.ai  ·  Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

        _logger.LogInformation("Generated PDF for report {ReportId} ({Size} bytes)", report.Id, bytes.Length);
        return Task.FromResult(bytes);
    }

    private static FullTaxReportDto? TryDeserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FullTaxReportDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Ngn(decimal value) => $"NGN {value.ToString("N2", Inv)}";

    private static void SectionTitle(ColumnDescriptor column, string title) =>
        column.Item().PaddingTop(6).Text(title.ToUpperInvariant()).FontSize(9).Bold().FontColor(Muted).LetterSpacing(0.05f);

    private static void KeyValueTable(ColumnDescriptor column, IEnumerable<(string Label, string Value)> rows)
    {
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(2);
            });

            foreach (var (label, value) in rows)
            {
                table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(5).Text(label);
                table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(5).AlignRight().Text(value);
            }
        });
    }

    private static void Bullets(ColumnDescriptor column, IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(12).Text("•").FontColor(Brand);
                row.RelativeItem().Text(item);
            });
        }
    }

    private static void RenderDetailed(ColumnDescriptor column, FullTaxReportDto r)
    {
        column.Item().Background("#F1F5F9").Padding(14).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Estimated tax payable").FontColor(Muted);
                c.Item().Text(Ngn(r.TaxPayable)).FontSize(22).Bold();
                c.Item().Text($"Tax year {r.TaxYear} · {r.RuleSet.Name}").FontSize(9).FontColor(Muted);
            });
            row.ConstantItem(150).AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text("Effective rate").FontColor(Muted);
                c.Item().AlignRight().Text(r.EffectiveTaxRate.ToString("P2", Inv)).FontSize(16).Bold();
                if (r.EstimatedMonthlyPaye.HasValue)
                {
                    c.Item().AlignRight().Text($"Monthly PAYE {Ngn(r.EstimatedMonthlyPaye.Value)}").FontSize(9).FontColor(Muted);
                }
            });
        });

        SectionTitle(column, "Summary");
        var rows = new List<(string, string)>
        {
            ("Gross income", Ngn(r.GrossIncome)),
            (r.TaxType == TaxTypes.CompanyIncomeTax ? "Assessable profit" : "Chargeable income", Ngn(r.AdjustedGrossIncome)),
            ("Reliefs and deductions", Ngn(r.TotalDeductionsAndReliefs)),
            ("Taxable income", Ngn(r.TaxableIncome)),
            ("Estimated tax payable", Ngn(r.TaxPayable))
        };
        KeyValueTable(column, rows);

        if (r.CompanyTax != null)
        {
            var c = r.CompanyTax;
            SectionTitle(column, "Company income tax");
            KeyValueTable(column, new[]
            {
                ("Classification", c.Classification),
                ("Turnover", Ngn(c.Turnover)),
                ("Allowable expenses", Ngn(c.AllowableExpenses)),
                ("Assessable profit", Ngn(c.AssessableProfit)),
                ($"Company income tax @ {c.IncomeTaxRate.ToString("P0", Inv)}", Ngn(c.IncomeTax)),
                ($"{c.LevyName} @ {c.LevyRate.ToString("P0", Inv)}", Ngn(c.Levy))
            });
        }

        if (r.DeductionAnalysis.Count > 0)
        {
            SectionTitle(column, "Deduction analysis");
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });
                table.Header(h =>
                {
                    h.Cell().PaddingVertical(4).Text("Item").Bold();
                    h.Cell().PaddingVertical(4).AlignRight().Text("Claimed").Bold();
                    h.Cell().PaddingVertical(4).AlignRight().Text("Allowed").Bold();
                });
                foreach (var line in r.DeductionAnalysis)
                {
                    table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4).Column(col =>
                    {
                        col.Item().Text(line.Label);
                        col.Item().Text(line.Note).FontSize(8).FontColor(Muted);
                    });
                    table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4).AlignRight().Text(Ngn(line.Claimed));
                    table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4).AlignRight().Text(Ngn(line.Allowed));
                }
            });
        }

        if (r.TaxBandBreakdown.Count > 0)
        {
            SectionTitle(column, "Tax band breakdown");
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });
                table.Header(h =>
                {
                    h.Cell().PaddingVertical(4).Text("Band").Bold();
                    h.Cell().PaddingVertical(4).AlignRight().Text("Income in band").Bold();
                    h.Cell().PaddingVertical(4).AlignRight().Text("Tax").Bold();
                });
                foreach (var band in r.TaxBandBreakdown)
                {
                    table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4).Text(band.Label.Replace("₦", "NGN "));
                    table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4).AlignRight().Text(Ngn(band.TaxableAmount));
                    table.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(4).AlignRight().Text(Ngn(band.TaxAmount));
                }
            });
        }

        if (r.CalculationSteps.Count > 0)
        {
            SectionTitle(column, "How this was calculated");
            foreach (var step in r.CalculationSteps)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(step.Label).SemiBold();
                        c.Item().Text(step.Description.Replace("₦", "NGN ")).FontSize(8).FontColor(Muted);
                    });
                    row.ConstantItem(120).AlignRight().Text(Ngn(step.Amount));
                });
            }
        }

        if (r.Vat.Applicable)
        {
            SectionTitle(column, "VAT position");
            column.Item().Text(r.Vat.StatusMessage.Replace("₦", "NGN "));
        }

        if (r.OptimizationTips.Count > 0)
        {
            SectionTitle(column, "Recommendations");
            Bullets(column, r.OptimizationTips.Select(t => t.Replace("₦", "NGN ")));
        }

        if (r.ComplianceWarnings.Count > 0)
        {
            SectionTitle(column, "Compliance points");
            Bullets(column, r.ComplianceWarnings.Select(t => t.Replace("₦", "NGN ")));
        }

        if (r.NextSteps.Count > 0)
        {
            SectionTitle(column, "Next steps");
            Bullets(column, r.NextSteps);
        }

        SectionTitle(column, "Assumptions and basis");
        Bullets(column, r.Assumptions.Select(a => a.Replace("₦", "NGN ")));
        column.Item().Text($"Legal basis: {r.RuleSet.LegalBasis}").FontSize(8).FontColor(Muted);
        if (r.RuleSet.VerificationStatus != VerificationStatuses.Verified)
        {
            column.Item().Text("Some rules in this rule set are pending validation against official publications and are marked as such in the app.")
                .FontSize(8).FontColor(Muted);
        }
    }

    private static void RenderBasic(ColumnDescriptor column, TaxReport report, string summary, string advice)
    {
        SectionTitle(column, "Summary");
        KeyValueTable(column, new[]
        {
            ("Total income", Ngn(report.TotalIncome)),
            ("Allowable deductions", Ngn(report.AllowableDeductions)),
            ("Tax liability", Ngn(report.TaxLiability))
        });
        column.Item().Text(summary);
        column.Item().Text(advice).FontColor(Muted);
    }
}
