namespace TaxNavi.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;

public class AzureAiTaxAdvisorService : IAiTaxAdvisorService
{
    private readonly string? _azureOpenAiKey;
    private readonly string? _azureOpenAiEndpoint;
    private readonly string? _deploymentName;

    public AzureAiTaxAdvisorService()
    {
        _azureOpenAiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        _azureOpenAiEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        _deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-35-turbo";
    }

    public async Task<string> GenerateHumanReadableSummaryAsync(TaxReport report)
    {
        try
        {
            // Azure OpenAI integration - implement when keys are available
            return GenerateFallbackSummary(report);
        }
        catch
        {
            return GenerateFallbackSummary(report);
        }
    }

    public async Task<string> GenerateSavingsAdviceAsync(decimal taxLiability, decimal income)
    {
        try
        {
            // Azure OpenAI integration - implement when keys are available
            return GenerateFallbackSavingsAdvice(taxLiability, income);
        }
        catch
        {
            return GenerateFallbackSavingsAdvice(taxLiability, income);
        }
    }

    public async Task<string> GenerateSMERoadmapAsync(SMETaxCalculationResult result)
    {
        try
        {
            // Azure OpenAI integration - implement when keys are available
            return result.ComplianceRoadmap;
        }
        catch
        {
            return result.ComplianceRoadmap;
        }
    }

    public Task<string> GenerateHumanReadableSummary(TaxReport report)
    {
        return Task.FromResult(GenerateFallbackSummary(report));
    }

    public Task<string> GenerateSavingsAdvice(decimal taxLiability, decimal income)
    {
        return Task.FromResult(GenerateFallbackSavingsAdvice(taxLiability, income));
    }

    public Task<string> GenerateSMERoadmap(SMETaxCalculationResult result)
    {
        return Task.FromResult(result.ComplianceRoadmap);
    }

    private string GenerateFallbackSummary(TaxReport report)
    {
        return $@"Tax Assessment Summary

Tax Liability: ₦{report.TaxLiability:N0}
Total Income: ₦{report.TotalIncome:N0}
Allowable Deductions: ₦{report.AllowableDeductions:N0}
Projected Savings: ₦{report.ProjectedSavings:N0}

Your calculated tax liability is based on current Nigerian tax regulations. 
Please consult with a qualified tax professional for personalized advice on tax optimization strategies.";
    }

    private string GenerateFallbackSavingsAdvice(decimal taxLiability, decimal income)
    {
        var effectiveRate = income > 0 ? (taxLiability / income) * 100 : 0;
        return $@"Personalized Tax Savings Advice

Your effective tax rate is {effectiveRate:F2}%.

Recommendations:
1. Ensure you claim all allowable deductions available to your employment/business status
2. Consider consulting with a tax professional about available tax relief schemes
3. Keep detailed records of all business expenses for potential deductions
4. Review your income sources to identify opportunities for tax-efficient structuring

Note: Please consult with a qualified tax advisor for specific recommendations.";
    }
}

public class BlobStorageService : IBlobStorageService
{
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(ILogger<BlobStorageService> logger)
    {
        _logger = logger;
        var connectionString = Environment.GetEnvironmentVariable("AZURE_BLOB_STORAGE_CONNECTION_STRING");
        var containerName = Environment.GetEnvironmentVariable("AZURE_BLOB_CONTAINER_NAME") ?? "tax-reports";
    }

    public async Task<string> UploadFileAsync(byte[] fileContent, string fileName, string containerName = "tax-reports")
    {
        try
        {
            _logger.LogInformation("Uploading file: {FileName} to container: {ContainerName}", fileName, containerName);

            // Save to local storage for development
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", containerName);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string filePath = Path.Combine(uploadsFolder, fileName);
            await File.WriteAllBytesAsync(filePath, fileContent);

            // Return mock URL for now (would be Azure Blob URL in production)
            return await Task.FromResult($"https://mock-blob-storage.example.com/{containerName}/{fileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", fileName);
            return $"https://mock-blob-storage.example.com/{containerName}/{fileName}";
        }
    }

    public async Task<byte[]> DownloadFileAsync(string fileName, string containerName = "tax-reports")
    {
        try
        {
            _logger.LogInformation("Downloading file: {FileName} from container: {ContainerName}", fileName, containerName);

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", containerName);
            string filePath = Path.Combine(uploadsFolder, fileName);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return Array.Empty<byte>();
            }

            return await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file: {FileName}", fileName);
            return Array.Empty<byte>();
        }
    }

    public async Task<bool> DeleteFileAsync(string fileName, string containerName = "tax-reports")
    {
        try
        {
            _logger.LogInformation("Deleting file: {FileName} from container: {ContainerName}", fileName, containerName);

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", containerName);
            string filePath = Path.Combine(uploadsFolder, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FileName}", fileName);
            return await Task.FromResult(false);
        }
    }
}
