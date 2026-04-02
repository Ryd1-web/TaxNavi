using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IPdfService _pdfService;
        private readonly ITaxReportRepository _reportRepository;
        private readonly IBlobStorageService _blobService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IPdfService pdfService,
            ITaxReportRepository reportRepository,
            IBlobStorageService blobService,
            ILogger<ReportsController> logger)
        {
            _pdfService = pdfService;
            _reportRepository = reportRepository;
            _blobService = blobService;
            _logger = logger;
        }

        [HttpPost("generate-pdf")]
        public async Task<IActionResult> GeneratePdfReport([FromBody] GeneratePdfReportDto request)
        {
            try
            {
                _logger.LogInformation("Generating PDF report for type: {ReportType}", request.ReportType);

                // Create a TaxReport object for PDF generation
                var taxReport = new Domain.Entities.TaxReport
                {
                    Id = Guid.NewGuid(),
                    TaxLiability = request.TaxLiability,
                    TotalIncome = request.TotalIncome,
                    AllowableDeductions = request.AllowableDeductions,
                    ProjectedSavings = request.ProjectedSavings,
                    CreatedAt = DateTime.UtcNow
                };

                // Generate PDF
                var pdfBytes = await _pdfService.GenerateTaxReportPdfAsync(
                    taxReport,
                    $"Tax Report - {request.ReportType}",
                    "Consult with a tax professional for optimization strategies"
                );

                // Upload to blob storage
                var fileName = $"tax-report-{taxReport.Id}.pdf";
                var blobUrl = await _blobService.UploadFileAsync(pdfBytes, fileName, "tax-reports");

                // Save report to database
                await _reportRepository.AddAsync(taxReport);
                await _reportRepository.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "PDF report generated successfully",
                    data = new
                    {
                        reportId = taxReport.Id,
                        pdfUrl = blobUrl,
                        fileName = fileName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF report");
                return StatusCode(500, new { success = false, message = "An error occurred while generating PDF report" });
            }
        }

        [HttpGet("{reportId}")]
        public async Task<IActionResult> GetReport(Guid reportId)
        {
            try
            {
                var report = await _reportRepository.GetByIdAsync(reportId);

                if (report == null)
                {
                    return NotFound(new { success = false, message = "Report not found" });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = report.Id,
                        pdfUrl = report.PdfUrl,
                        summary = report.Summary,
                        createdAt = report.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report");
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving report" });
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserReports(Guid userId)
        {
            try
            {
                var reports = await _reportRepository.GetByUserIdAsync(userId);

                return Ok(new
                {
                    success = true,
                    data = reports.Select(r => new
                    {
                        id = r.Id,
                        pdfUrl = r.PdfUrl,
                        summary = r.Summary,
                        createdAt = r.CreatedAt
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user reports");
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving user reports" });
            }
        }

        [HttpDelete("{reportId}")]
        public async Task<IActionResult> DeleteReport(Guid reportId)
        {
            try
            {
                var report = await _reportRepository.GetByIdAsync(reportId);

                if (report == null)
                {
                    return NotFound(new { success = false, message = "Report not found" });
                }

                await _reportRepository.DeleteAsync(reportId);
                await _reportRepository.SaveChangesAsync();

                return Ok(new { success = true, message = "Report deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting report");
                return StatusCode(500, new { success = false, message = "An error occurred while deleting report" });
            }
        }
    }
}
