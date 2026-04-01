namespace TaxNavi.Infrastructure.Services;

using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using System.Text;

public class PdfReportService : IPdfService
{
    private readonly BlobStorageService _blobStorageService;

    public PdfReportService(BlobStorageService blobStorageService)
    {
        _blobStorageService = blobStorageService;
    }

    public async Task<byte[]> GenerateTaxReportPdfAsync(TaxReport report, string summary, string advice)
    {
        // Create a simple PDF-like text file for now
        // In production, use actual PDF library like iTextSharp
        var content = GenerateReportContent(report, summary, advice);
        var bytes = Encoding.UTF8.GetBytes(content);

        var fileName = $"tax-report-{report.Id}.pdf";
        await _blobStorageService.UploadFileAsync(bytes, fileName);

        return bytes;
    }

    public async Task<string> UploadPdfToBlobStorageAsync(byte[] pdfContent, string fileName)
    {
        return await _blobStorageService.UploadFileAsync(pdfContent, fileName);
    }

    private string GenerateReportContent(TaxReport report, string summary, string advice)
    {
        var sb = new StringBuilder();
        sb.AppendLine("===============================================");
        sb.AppendLine("              TAXNAVI.AI TAX REPORT");
        sb.AppendLine("===============================================");
        sb.AppendLine();
        sb.AppendLine($"Report ID: {report.Id}");
        sb.AppendLine($"Generated: {report.CreatedAt:F}");
        sb.AppendLine();
        sb.AppendLine("TAX ASSESSMENT SUMMARY");
        sb.AppendLine("-------------------");
        sb.AppendLine($"Total Income:              ₦{report.TotalIncome:N0}");
        sb.AppendLine($"Allowable Deductions:      ₦{report.AllowableDeductions:N0}");
        sb.AppendLine($"Tax Liability:             ₦{report.TaxLiability:N0}");
        sb.AppendLine($"Projected Savings:         ₦{report.ProjectedSavings:N0}");
        sb.AppendLine();
        sb.AppendLine("AI-GENERATED SUMMARY");
        sb.AppendLine("-------------------");
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine("TAX OPTIMIZATION RECOMMENDATIONS");
        sb.AppendLine("--------------------------------");
        sb.AppendLine(advice);
        sb.AppendLine();
        sb.AppendLine("===============================================");
        sb.AppendLine("This report is for informational purposes.");
        sb.AppendLine("Please consult with a qualified tax professional.");
        sb.AppendLine("===============================================");
        return sb.ToString();
    }
}

public class PaymentService : IPaymentService
{
    private readonly string? _paystackSecretKey;

    public PaymentService()
    {
        _paystackSecretKey = Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
    }

    public async Task<PaymentInitiationResponse> InitiatePaymentAsync(PaymentRequest request)
    {
        // Mock Paystack integration
        // In production, use actual Paystack API
        var reference = $"PAY-{DateTime.UtcNow.Ticks}";

        var response = new PaymentInitiationResponse
        {
            Reference = reference,
            AccessCode = GenerateAccessCode(),
            AuthorizationUrl = $"https://checkout.paystack.com/mock/{reference}"
        };

        return await Task.FromResult(response);
    }

    public async Task<PaymentVerificationResponse> VerifyPaymentAsync(string reference)
    {
        // Mock Paystack verification
        // In production, call actual Paystack API
        var response = new PaymentVerificationResponse
        {
            Success = true,
            Status = "success",
            Reference = reference
        };

        return await Task.FromResult(response);
    }

    private string GenerateAccessCode()
    {
        return Guid.NewGuid().ToString("N")[..12].ToUpper();
    }
}
