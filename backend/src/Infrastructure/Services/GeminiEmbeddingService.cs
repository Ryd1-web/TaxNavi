using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;

namespace TaxNavi.Infrastructure.Services;

/// <summary>
/// Embeddings over the Gemini REST API. Queries and documents use asymmetric task types,
/// vectors are requested at the stored dimension and L2-normalised (required when a
/// Matryoshka embedding is truncated below its native size) so cosine similarity is exact.
/// </summary>
public sealed class GeminiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiEmbeddingService> _logger;

    public GeminiEmbeddingService(HttpClient http, GeminiOptions options, ILogger<GeminiEmbeddingService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
        if (IsConfigured && !_http.DefaultRequestHeaders.Contains("x-goog-api-key"))
        {
            _http.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);
    public string ModelName => _options.EmbeddingModel;

    public async Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            content = new { parts = new[] { new { text } } },
            taskType = "RETRIEVAL_QUERY",
            outputDimensionality = DocumentChunk.EmbeddingDimensions
        };

        if (GeminiRetry.InCooldown)
        {
            throw new HttpRequestException("Embedding provider quota exhausted (cooling down).");
        }

        // A user is waiting: short retries only.
        using var response = await GeminiRetry.SendAsync(
            () => _http.PostAsJsonAsync($"models/{Uri.EscapeDataString(ModelName)}:embedContent", body, cancellationToken),
            _options.MaxRetries,
            TimeSpan.FromSeconds(_options.MaxChatRetryWaitSeconds),
            TimeSpan.FromMinutes(_options.QuotaCooldownMinutes),
            _logger,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Normalize(ReadValues(json.RootElement.GetProperty("embedding")));
    }

    public async Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new List<float[]>(texts.Count);
        for (var offset = 0; offset < texts.Count; offset += _options.EmbeddingBatchSize)
        {
            var batch = texts.Skip(offset).Take(_options.EmbeddingBatchSize).ToArray();
            var body = new
            {
                requests = batch.Select(t => new
                {
                    model = $"models/{ModelName}",
                    content = new { parts = new[] { new { text = t } } },
                    taskType = "RETRIEVAL_DOCUMENT",
                    outputDimensionality = DocumentChunk.EmbeddingDimensions
                }).ToArray()
            };

            if (offset > 0 && _options.EmbeddingBatchDelayMs > 0)
            {
                await Task.Delay(_options.EmbeddingBatchDelayMs, cancellationToken); // stay under per-minute limits
            }

            // Background indexing: honour long server-suggested waits rather than failing the document.
            using var response = await GeminiRetry.SendAsync(
                () => _http.PostAsJsonAsync($"models/{Uri.EscapeDataString(ModelName)}:batchEmbedContents", body, cancellationToken),
                _options.MaxRetries + 4,
                TimeSpan.FromSeconds(_options.MaxIndexRetryWaitSeconds),
                TimeSpan.FromMinutes(_options.QuotaCooldownMinutes),
                _logger,
                cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            foreach (var embedding in json.RootElement.GetProperty("embeddings").EnumerateArray())
            {
                results.Add(Normalize(ReadValues(embedding)));
            }
        }

        if (results.Count != texts.Count)
        {
            throw new InvalidOperationException($"Embedding count mismatch: sent {texts.Count}, received {results.Count}.");
        }

        return results;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Surface the status, never the payload (it can echo input text).
        _logger.LogError("Gemini embedding call failed: {Status}", (int)response.StatusCode);
        _ = await response.Content.ReadAsStringAsync(cancellationToken); // drain
        throw new HttpRequestException($"Embedding provider returned {(int)response.StatusCode}.");
    }

    private static float[] ReadValues(JsonElement embedding)
    {
        var values = embedding.GetProperty("values");
        var vector = new float[values.GetArrayLength()];
        var i = 0;
        foreach (var value in values.EnumerateArray())
        {
            vector[i++] = value.GetSingle();
        }

        return vector;
    }

    private static float[] Normalize(float[] vector)
    {
        double sum = 0;
        foreach (var v in vector)
        {
            sum += (double)v * v;
        }

        var norm = Math.Sqrt(sum);
        if (norm < 1e-12)
        {
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }
}
