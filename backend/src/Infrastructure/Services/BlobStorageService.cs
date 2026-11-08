namespace TaxNavi.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using TaxNavi.Domain.Interfaces;

/// <summary>Local-disk storage used in development. Swap for Azure Blob Storage in production.</summary>
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
