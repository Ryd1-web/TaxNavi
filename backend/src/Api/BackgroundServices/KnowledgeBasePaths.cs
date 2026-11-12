namespace TaxNavi.Api.BackgroundServices;

/// <summary>Locates files in the knowledge-base folder for both `dotnet run` (src/Api) and the Docker image (/app).</summary>
public static class KnowledgeBasePaths
{
    public static string? Resolve(IConfiguration configuration, IHostEnvironment environment, string fileName, string? configuredKey = null)
    {
        var candidates = new List<string>();
        var configured = configuredKey == null ? null : configuration[configuredKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            candidates.Add(configured);
        }

        candidates.Add(Path.Combine(environment.ContentRootPath, "knowledge-base", fileName));          // Docker image / published
        candidates.Add(Path.Combine(environment.ContentRootPath, "..", "..", "knowledge-base", fileName)); // dotnet run from src/Api
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
    }
}
