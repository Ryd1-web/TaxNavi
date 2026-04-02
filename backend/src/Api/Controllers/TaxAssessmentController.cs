using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TaxNavi.Application.DTOs;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Domain.Entities;
using System.Security.Claims;
using System.Text.Json;

namespace TaxNavi.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TaxAssessmentController : ControllerBase
    {
        private readonly ITaxCalculationService _taxService;
        private readonly IAiTaxAdvisorService _aiService;
        private readonly ITaxProfileRepository _profileRepository;
        private readonly ITaxReportRepository _reportRepository;
        private readonly ILogger<TaxAssessmentController> _logger;

        public TaxAssessmentController(
            ITaxCalculationService taxService,
            IAiTaxAdvisorService aiService,
            ITaxProfileRepository profileRepository,
            ITaxReportRepository reportRepository,
            ILogger<TaxAssessmentController> logger)
        {
            _taxService = taxService;
            _aiService = aiService;
            _profileRepository = profileRepository;
            _reportRepository = reportRepository;
            _logger = logger;
        }

        [HttpPost("individual")]
        public async Task<IActionResult> AssessIndividualTax([FromBody] IndividualTaxAssessmentDto assessment)
        {
            try
            {
                _logger.LogInformation("Tax assessment for individual user: {UserId}", assessment.UserId);

                // Calculate tax
                var taxLiability = _taxService.CalculatePIT(
                    assessment.MonthlySalary * 12 + assessment.AnnualBonus + assessment.Allowances,
                    assessment.RentPaid + assessment.LoanInterest + assessment.OtherExpenses
                );

                var consolidatedRelief = _taxService.CalculateConsolidatedRelief(
                    assessment.MonthlySalary * 12 + assessment.AnnualBonus + assessment.Allowances
                );

                var totalIncome = assessment.MonthlySalary * 12 + assessment.AnnualBonus + assessment.Allowances + 
                                assessment.BusinessIncome + assessment.FreelanceIncome + assessment.InvestmentIncome;
                
                var allowableDeductions = assessment.RentPaid + assessment.LoanInterest + assessment.OtherExpenses;
                var projectedSavings = consolidatedRelief > 0 ? consolidatedRelief * 0.1m : 0;

                // Create tax profile
                var profile = new TaxProfile
                {
                    UserId = assessment.UserId,
                    ProfileType = "Individual",
                    JsonData = JsonSerializer.Serialize(assessment),
                    CreatedAt = DateTime.UtcNow
                };

                await _profileRepository.AddAsync(profile);
                await _profileRepository.SaveChangesAsync();

                // Create tax report
                var report = new TaxReport
                {
                    UserId = assessment.UserId,
                    TaxProfileId = profile.Id,
                    ReportType = "Individual",
                    TotalIncome = totalIncome,
                    AllowableDeductions = allowableDeductions,
                    TaxLiability = taxLiability,
                    ProjectedSavings = projectedSavings,
                    JsonData = JsonSerializer.Serialize(assessment),
                    CreatedAt = DateTime.UtcNow
                };

                await _reportRepository.AddAsync(report);
                await _reportRepository.SaveChangesAsync();

                // Generate AI summary
                var aiSummary = await _aiService.GenerateHumanReadableSummaryAsync(report);
                var aiAdvice = await _aiService.GenerateSavingsAdviceAsync(taxLiability, totalIncome);

                return Ok(new
                {
                    success = true,
                    message = "Individual tax assessment completed successfully",
                    data = new
                    {
                        reportId = report.Id,
                        totalIncome = totalIncome,
                        allowableDeductions = allowableDeductions,
                        taxLiability = taxLiability,
                        consolidatedRelief = consolidatedRelief,
                        projectedSavings = projectedSavings,
                        summary = aiSummary,
                        advice = aiAdvice
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during individual tax assessment");
                return StatusCode(500, new { success = false, message = "An error occurred during tax assessment" });
            }
        }

        [HttpPost("sme")]
        public async Task<IActionResult> AssessSMETax([FromBody] SMEAssessmentDto assessment)
        {
            try
            {
                _logger.LogInformation("Tax assessment for SME user: {UserId}", assessment.UserId);

                var totalIncome = assessment.AnnualTurnover;
                var allowableDeductions = assessment.OperatingExpenses + assessment.PayrollCost;
                
                // Determine VAT eligibility (turnover > 25 million)
                var isVatEligible = assessment.AnnualTurnover > 25_000_000;
                var vatLiability = isVatEligible ? assessment.VatableIncome * 0.075m : 0;
                var whtLiability = assessment.WhtExposure * 0.05m;
                var totalTaxLiability = vatLiability + whtLiability;

                // Create tax profile
                var profile = new TaxProfile
                {
                    UserId = assessment.UserId,
                    ProfileType = "SME",
                    JsonData = JsonSerializer.Serialize(assessment),
                    CreatedAt = DateTime.UtcNow
                };

                await _profileRepository.AddAsync(profile);
                await _profileRepository.SaveChangesAsync();

                // Create tax report
                var report = new TaxReport
                {
                    UserId = assessment.UserId,
                    TaxProfileId = profile.Id,
                    ReportType = "SME",
                    TotalIncome = totalIncome,
                    AllowableDeductions = allowableDeductions,
                    TaxLiability = totalTaxLiability,
                    ProjectedSavings = totalTaxLiability * 0.15m,
                    JsonData = JsonSerializer.Serialize(assessment),
                    CreatedAt = DateTime.UtcNow
                };

                await _reportRepository.AddAsync(report);
                await _reportRepository.SaveChangesAsync();

                // Generate AI roadmap
                var aiRoadmap = await _aiService.GenerateSMERoadmapAsync(new Domain.Interfaces.SMETaxCalculationResult
                {
                    VatEligible = isVatEligible,
                    VatLiability = vatLiability,
                    WhtLiability = whtLiability,
                    TotalTaxObligation = totalTaxLiability,
                    ComplianceRoadmap = "Annual VAT filing, WHT compliance, Quarterly returns"
                });

                var aiSummary = await _aiService.GenerateHumanReadableSummaryAsync(report);

                return Ok(new
                {
                    success = true,
                    message = "SME tax assessment completed successfully",
                    data = new
                    {
                        reportId = report.Id,
                        isVatEligible = isVatEligible,
                        vatLiability = vatLiability,
                        whtLiability = whtLiability,
                        totalTaxLiability = totalTaxLiability,
                        projectedSavings = report.ProjectedSavings,
                        complianceRoadmap = aiRoadmap,
                        summary = aiSummary
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SME tax assessment");
                return StatusCode(500, new { success = false, message = "An error occurred during SME tax assessment" });
            }
        }

        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetTaxHistory(Guid userId)
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
                        type = r.ReportType,
                        taxLiability = r.TaxLiability,
                        createdAt = r.CreatedAt,
                        pdfUrl = r.PdfUrl
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving tax history");
                return StatusCode(500, new { success = false, message = "An error occurred while retrieving tax history" });
            }
        }
    }
}
