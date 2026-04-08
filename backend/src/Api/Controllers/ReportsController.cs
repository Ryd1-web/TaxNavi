using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Interfaces;
using System.Security.Claims;

namespace TaxNavi.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IPdfService _pdfService;
        private readonly ITaxReportRepository _reportRepository;
        private readonly IUserRepository _userRepository;
        private readonly IBlobStorageService _blobService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IPdfService pdfService,
            ITaxReportRepository reportRepository,
            IUserRepository userRepository,
            IBlobStorageService blobService,
            ILogger<ReportsController> logger)
        {
            _pdfService = pdfService;
            _reportRepository = reportRepository;
            _userRepository = userRepository;
            _blobService = blobService;
            _logger = logger;
        }

        [HttpPost("generate-pdf")]
        public async Task<IActionResult> GeneratePdfReport([FromBody] GeneratePdfReportDto request)
        {
            try
            {
                _logger.LogInformation("Generating PDF report for type: {ReportType}", request.ReportType);

                // Get UserId from JWT claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new { success = false, message = "Invalid or missing user ID in token" });
                }

                // Fetch user to get name
                var user = await _userRepository.GetByIdAsync(userId);
                var userName = user != null ? $"{user.FirstName} {user.LastName}" : "Taxpayer";

                // Create a TaxReport object for PDF generation
                var reportId = Guid.NewGuid();
                var taxReport = new Domain.Entities.TaxReport
                {
                    Id = reportId,
                    UserId = userId,
                    TaxLiability = request.TaxLiability,
                    TotalIncome = request.TotalIncome,
                    AllowableDeductions = request.AllowableDeductions,
                    ProjectedSavings = request.ProjectedSavings,
                    ReportType = request.ReportType,
                    CreatedAt = DateTime.UtcNow
                };

                // Generate PDF with user name
                var pdfBytes = await _pdfService.GenerateTaxReportPdfAsync(
                    taxReport,
                    $"Tax Report - {request.ReportType}",
                    "Consult with a tax professional for optimization strategies",
                    userName
                );

                // Upload to blob storage
                var fileName = $"tax-report-{reportId}.pdf";
                var blobUrl = await _blobService.UploadFileAsync(pdfBytes, fileName, "tax-reports");

                // Save report to database (without foreign key dependencies)
                try
                {
                    taxReport.PdfUrl = $"/api/reports/download/{reportId}";
                    taxReport.Summary = "Tax Report Generated";
                    await _reportRepository.AddAsync(taxReport);
                    await _reportRepository.SaveChangesAsync();
                    _logger.LogInformation("Report saved to database: {ReportId}", reportId);
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "Failed to save report to database, but PDF generation succeeded");
                    // Continue even if database save fails - PDF is already generated
                }

                // Return PDF details with direct download URL
                var downloadUrl = $"/api/reports/download/{reportId}";
                return Ok(new
                {
                    success = true,
                    message = "PDF report generated successfully",
                    data = new
                    {
                        reportId = reportId,
                        pdfUrl = downloadUrl,
                        fileName = fileName
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF report");
                return StatusCode(500, new { success = false, message = $"An error occurred while generating PDF report: {ex.Message}" });
            }
        }

        [HttpGet("download/{id}")]
        public async Task<IActionResult> DownloadReport(Guid id)
        {
            try
            {
                // First, try to get the report from the database
                var report = await _reportRepository.GetByIdAsync(id);
                if (report == null)
                {
                    return NotFound(new { success = false, message = "Report not found" });
                }

                var fileName = $"tax-report-{id}.pdf";

                // Try to download from blob storage
                var fileBytes = await _blobService.DownloadFileAsync(fileName, "tax-reports");

                // If file doesn't exist in blob storage, regenerate it from the stored report data
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _logger.LogInformation("PDF file not found in storage, regenerating for report {ReportId}", id);

                    // Get user info for the PDF header
                    var user = await _userRepository.GetByIdAsync(report.UserId);
                    var userName = user != null ? $"{user.FirstName} {user.LastName}" : "Taxpayer";

                    // Regenerate PDF from stored report data
                    fileBytes = await _pdfService.GenerateTaxReportPdfAsync(
                        report,
                        $"Tax Report - {report.ReportType}",
                        report.Summary ?? "Consult with a tax professional for optimization strategies",
                        userName
                    );

                    if (fileBytes == null || fileBytes.Length == 0)
                    {
                        return StatusCode(500, new { success = false, message = "Failed to generate PDF report" });
                    }

                    // Save the regenerated PDF to blob storage for future downloads
                    try
                    {
                        await _blobService.UploadFileAsync(fileBytes, fileName, "tax-reports");
                        _logger.LogInformation("Regenerated PDF saved to storage for report {ReportId}", id);
                    }
                    catch (Exception storageEx)
                    {
                        _logger.LogWarning(storageEx, "Failed to save regenerated PDF to storage");
                        // Continue anyway - we have the PDF bytes to return
                    }
                }

                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading PDF report");
                return StatusCode(500, new { success = false, message = $"An error occurred while downloading PDF report: {ex.Message}" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReport(Guid id)
        {
            try
            {
                var report = await _reportRepository.GetByIdAsync(id);

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
                        userId = report.UserId,
                        createdAt = report.CreatedAt,
                        reportType = report.ReportType,
                        totalIncome = report.TotalIncome,
                        taxLiability = report.TaxLiability,
                        allowableDeductions = report.AllowableDeductions,
                        projectedSavings = report.ProjectedSavings,
                        pdfUrl = report.PdfUrl ?? $"/api/reports/download/{report.Id}",
                        summary = report.Summary
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReport(Guid id)
        {
            try
            {
                var report = await _reportRepository.GetByIdAsync(id);

                if (report == null)
                {
                    return NotFound(new { success = false, message = "Report not found" });
                }

                await _reportRepository.DeleteAsync(id);
                await _reportRepository.SaveChangesAsync();

                return Ok(new { success = true, message = "Report deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting report");
                return StatusCode(500, new { success = false, message = "An error occurred while deleting report" });
            }
        }

        [HttpGet("user")]
        [Authorize]
        public async Task<IActionResult> GetUserReports()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new { success = false, message = "Invalid or missing user ID in token" });
                }

                var reports = await _reportRepository.GetByUserIdAsync(userId);

                var reportDtos = reports.Select(r => new
                {
                    id = r.Id,
                    userId = r.UserId,
                    createdAt = r.CreatedAt,
                    reportType = r.ReportType,
                    totalIncome = r.TotalIncome,
                    taxLiability = r.TaxLiability,
                    allowableDeductions = r.AllowableDeductions,
                    projectedSavings = r.ProjectedSavings,
                    pdfUrl = r.PdfUrl ?? $"/api/reports/download/{r.Id}"
                }).OrderByDescending(r => r.createdAt).ToList();

                return Ok(new { success = true, data = reportDtos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user reports");
                return StatusCode(500, new { success = false, message = "An error occurred while fetching reports" });
            }
        }
    }
}
