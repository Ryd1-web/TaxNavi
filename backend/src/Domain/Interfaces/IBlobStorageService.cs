namespace TaxNavi.Domain.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(byte[] fileContent, string fileName, string containerName = "tax-reports");
    Task<byte[]> DownloadFileAsync(string fileName, string containerName = "tax-reports");
    Task<bool> DeleteFileAsync(string fileName, string containerName = "tax-reports");
}
