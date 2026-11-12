using System.Security.Cryptography;
using System.Text.Json;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Api.BackgroundServices;

/// <summary>
/// Seeds the knowledge base from knowledge-base/manifest.json once at startup, after migrations.
/// Idempotent: a file whose SHA-256 is already registered is skipped, and a registered-but-not-indexed
/// document is (re)indexed. Runs in the background so startup and health checks are never blocked.
/// </summary>
public sealed class KnowledgeBaseSeeder : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<KnowledgeBaseSeeder> _logger;

    public KnowledgeBaseSeeder(IServiceScopeFactory scopes, IConfiguration configuration, IHostEnvironment environment, ILogger<KnowledgeBaseSeeder> logger)
    {
        _scopes = scopes;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    private sealed record ManifestEntry(string File, string Title, string DocumentType, string SourceName, string SourceUrl,
        DateTime? PublicationDate, DateTime? EffectiveDate, int? TaxYear);

    private sealed record Manifest(List<ManifestEntry> Documents);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("KnowledgeBase:SeedOnStartup"))
        {
            return;
        }

        var manifestPath = KnowledgeBasePaths.Resolve(_configuration, _environment, "manifest.json", "KnowledgeBase:ManifestPath");
        if (manifestPath == null)
        {
            _logger.LogWarning("Knowledge base seeding enabled but no manifest.json was found");
            return;
        }

        // Give migrations (run synchronously at startup) and the web host a moment.
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        using var scope = _scopes.CreateScope();
        var embeddings = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        if (!embeddings.IsConfigured)
        {
            _logger.LogWarning("Knowledge base seeding skipped: embedding provider is not configured (set Gemini:ApiKey / GEMINI_API_KEY)");
            return;
        }

        var ingestion = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();
        var documents = scope.ServiceProvider.GetRequiredService<IKnowledgeDocumentRepository>();
        var baseDirectory = Path.GetDirectoryName(manifestPath)!;

        Manifest manifest;
        await using (var stream = File.OpenRead(manifestPath))
        {
            manifest = await JsonSerializer.DeserializeAsync<Manifest>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web), stoppingToken)
                ?? new Manifest(new List<ManifestEntry>());
        }

        // Free-tier quotas can reject a run part-way; failed documents are retried in later passes
        // (and again on the next boot), never left half-indexed.
        const int maxPasses = 4;
        for (var pass = 1; pass <= maxPasses; pass++)
        {
            var failures = 0;
            foreach (var entry in manifest.Documents)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                if (!await SeedOneAsync(entry, baseDirectory, ingestion, documents, stoppingToken))
                {
                    failures++;
                }
            }

            if (failures == 0 || pass == maxPasses)
            {
                if (failures > 0)
                {
                    _logger.LogWarning("Knowledge base seeding finished with {Failures} document(s) not indexed; they will be retried on next start", failures);
                }

                return;
            }

            var wait = TimeSpan.FromMinutes(15 * pass);
            _logger.LogInformation("Knowledge base seeding: {Failures} document(s) failed; retrying in {Minutes} min", failures, wait.TotalMinutes);
            await Task.Delay(wait, stoppingToken);
        }
    }

    private async Task<bool> SeedOneAsync(ManifestEntry entry, string baseDirectory, IDocumentIngestionService ingestion, IKnowledgeDocumentRepository documents, CancellationToken stoppingToken)
    {
        {

            try
            {
                var path = Path.GetFullPath(Path.Combine(baseDirectory, entry.File));
                if (!File.Exists(path))
                {
                    _logger.LogWarning("Knowledge base seed file missing: {File}", entry.File);
                    return true; // nothing to retry
                }

                var bytes = await File.ReadAllBytesAsync(path, stoppingToken);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                var existing = await documents.FindByContentHashAsync(hash, stoppingToken);
                if (existing != null && existing.Status is KnowledgeDocumentStatuses.Indexed or KnowledgeDocumentStatuses.Superseded)
                {
                    return true; // already in the knowledge base
                }

                var document = existing ?? await ingestion.RegisterAsync(new KnowledgeDocument
                {
                    Title = entry.Title,
                    DocumentType = entry.DocumentType,
                    SourceName = entry.SourceName,
                    SourceUrl = entry.SourceUrl,
                    PublicationDate = entry.PublicationDate,
                    EffectiveDate = entry.EffectiveDate,
                    TaxYear = entry.TaxYear,
                    FileName = Path.GetFileName(path)
                }, bytes, stoppingToken);

                _logger.LogInformation("Indexing knowledge document \"{Title}\" (v{Version})…", document.Title, document.Version);
                var indexed = await ingestion.IndexAsync(document.Id, stoppingToken);
                _logger.LogInformation("Indexed \"{Title}\": {Pages} pages → {Chunks} chunks", indexed.Title, indexed.PageCount, indexed.ChunkCount);
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return true;
            }
            catch (Exception ex)
            {
                // One bad document must not stop the others; its Failed status is visible in the admin list.
                _logger.LogError(ex, "Knowledge base seeding failed for \"{Title}\"", entry.Title);
                return false;
            }
        }
    }

}
