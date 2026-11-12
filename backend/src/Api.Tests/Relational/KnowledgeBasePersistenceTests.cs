using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TaxNavi.Domain.Entities;
using TaxNavi.Domain.Interfaces;
using TaxNavi.Infrastructure.Data;
using TaxNavi.Infrastructure.Repositories;
using TaxNavi.Infrastructure.Services;
using Xunit;

namespace TaxNavi.Api.Tests.Relational;

/// <summary>pgvector storage, similarity ranking, document versioning and the ingest pipeline on real PostgreSQL.</summary>
public class KnowledgeBasePersistenceTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _db;

    public KnowledgeBasePersistenceTests(PostgresFixture db) => _db = db;

    /// <summary>Deterministic "embeddings": a unit vector direction per known phrase.</summary>
    private sealed class StubEmbeddings : IEmbeddingService
    {
        public bool IsConfigured => true;
        public string ModelName => "stub";

        public Task<float[]> EmbedQueryAsync(string text, CancellationToken ct = default) => Task.FromResult(Embed(text));

        public Task<IReadOnlyList<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());

        public static float[] Embed(string text)
        {
            var vector = new float[DocumentChunk.EmbeddingDimensions];
            // Direction 0 = rent, 1 = vat, 2 = everything else; slight mix keeps cosine informative.
            var axis = text.Contains("rent", StringComparison.OrdinalIgnoreCase) ? 0
                : text.Contains("vat", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            vector[axis] = 0.95f;
            vector[(axis + 1) % 3] = 0.31f;
            var norm = MathF.Sqrt(vector.Sum(v => v * v));
            return vector.Select(v => v / norm).ToArray();
        }
    }

    private static KnowledgeDocument NewDocument(string title = "Nigeria Tax Act 2025") => new()
    {
        Title = title,
        DocumentType = KnowledgeDocumentTypes.Legislation,
        SourceName = "National Assembly",
        SourceUrl = "https://nass.gov.ng/example",
        TaxYear = 2026,
        FileName = "act.pdf"
    };

    private static DocumentChunk NewChunk(Guid documentId, int index, string content) => new()
    {
        Id = Guid.NewGuid(),
        DocumentId = documentId,
        ChunkIndex = index,
        Content = content,
        SectionNumber = (index + 1).ToString(),
        PageFrom = index + 1,
        PageTo = index + 1,
        TokenCount = content.Length / 4,
        Embedding = new Vector(StubEmbeddings.Embed(content)),
        CreatedAt = DateTime.UtcNow
    };

    private static byte[] LegislationPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(QuestPDF.Helpers.PageSizes.A4);
            page.Content().Column(col =>
            {
                col.Item().Text("PART I — PRELIMINARY");
                col.Item().Text("1.—(1) This Act may be cited as the Test Tax Act and applies to every taxable person in Nigeria for each year of assessment beginning with the 2026 tax year.");
                col.Item().Text("2. Rent relief");
                col.Item().Text("(1) A rent relief of twenty percent of annual rent paid, subject to a cap, shall be allowed as a deduction against the total income of every eligible individual under this Act.");
            });
        })).GeneratePdf();
    }

    [PostgresFact]
    public async Task VectorSearch_RanksByCosineSimilarity_AndFiltersUnindexedDocuments()
    {
        await using var db = _db.NewContext();
        var documents = new KnowledgeDocumentRepository(db);
        var vectors = new DocumentChunkRepository(db);

        var indexed = NewDocument();
        indexed.Id = Guid.NewGuid();
        indexed.ContentHash = Guid.NewGuid().ToString("N");
        indexed.Status = KnowledgeDocumentStatuses.Indexed;
        await documents.AddAsync(indexed);

        var pending = NewDocument("Draft guidance");
        pending.Id = Guid.NewGuid();
        pending.ContentHash = Guid.NewGuid().ToString("N");
        pending.Status = KnowledgeDocumentStatuses.Pending;
        await documents.AddAsync(pending);

        await vectors.ReplaceDocumentChunksAsync(indexed.Id, new[]
        {
            NewChunk(indexed.Id, 0, "Rent relief is allowed at twenty percent of annual rent."),
            NewChunk(indexed.Id, 1, "VAT registration is required above the turnover threshold.")
        });
        await vectors.ReplaceDocumentChunksAsync(pending.Id, new[]
        {
            NewChunk(pending.Id, 0, "Draft rent guidance that must never be retrieved.")
        });

        var hits = await vectors.SearchAsync(StubEmbeddings.Embed("How does rent relief work?"), topK: 5, minSimilarity: 0.2);

        Assert.Equal(2, hits.Count); // pending document excluded
        Assert.Contains("Rent relief", hits[0].Chunk.Content);
        Assert.True(hits[0].Similarity > hits[1].Similarity);
        Assert.Equal("Nigeria Tax Act 2025", hits[0].Document.Title);
        Assert.All(hits, h => Assert.True(h.Similarity is > 0 and <= 1.0001));
    }

    [PostgresFact]
    public async Task Ingestion_EndToEnd_RegistersIndexesAndSupersedes()
    {
        var pdf = LegislationPdf();

        await using var db = _db.NewContext();
        var documents = new KnowledgeDocumentRepository(db);
        var vectors = new DocumentChunkRepository(db);
        var ingestion = new DocumentIngestionService(documents, vectors, new StubEmbeddings(),
            new EfUnitOfWork(db), TimeProvider.System, NullLogger<DocumentIngestionService>.Instance);

        var title = $"Test Tax Act {Guid.NewGuid():N}";
        var v1 = await ingestion.RegisterAsync(NewDocument(title), pdf);
        Assert.Equal(1, v1.Version);
        Assert.Equal(KnowledgeDocumentStatuses.Pending, v1.Status);

        // Same bytes → the same document comes back, no duplicate row.
        var duplicate = await ingestion.RegisterAsync(NewDocument(title), pdf);
        Assert.Equal(v1.Id, duplicate.Id);

        var indexedV1 = await ingestion.IndexAsync(v1.Id);
        Assert.Equal(KnowledgeDocumentStatuses.Indexed, indexedV1.Status);
        Assert.True(indexedV1.ChunkCount >= 2, $"expected structural chunks, got {indexedV1.ChunkCount}");
        Assert.True(indexedV1.PageCount >= 1);

        // Retrieval finds the rent-relief section with its citation fields.
        var hits = await vectors.SearchAsync(StubEmbeddings.Embed("rent relief"), topK: 3, minSimilarity: 0.3);
        Assert.Contains(hits, h => h.Chunk.Content.Contains("twenty percent", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(hits, h => h.Chunk.SectionNumber == "2");

        // An amended version (different bytes) supersedes v1 only once indexed.
        var amended = LegislationPdf().Concat(new byte[] { 0x20 }).ToArray();
        var v2 = await ingestion.RegisterAsync(NewDocument(title), amended);
        Assert.Equal(2, v2.Version);
        await ingestion.IndexAsync(v2.Id);

        await using var check = _db.NewContext();
        var statuses = await check.KnowledgeDocuments.Where(d => d.Title == title)
            .ToDictionaryAsync(d => d.Version, d => d.Status);
        Assert.Equal(KnowledgeDocumentStatuses.Superseded, statuses[1]);
        Assert.Equal(KnowledgeDocumentStatuses.Indexed, statuses[2]);

        // Only the current version's chunks are searchable.
        var afterSupersede = await vectors.SearchAsync(StubEmbeddings.Embed("rent relief"), topK: 10, minSimilarity: 0.0);
        Assert.All(afterSupersede.Where(h => h.Document.Title == title), h => Assert.Equal(v2.Id, h.Document.Id));
    }

    [PostgresFact]
    public async Task Conversations_EnforceOwnership_AndMessageOrder()
    {
        await using var db = _db.NewContext();
        var repo = new ChatConversationRepository(db);
        var user = TestData.NewUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var conversation = new ChatConversation { Id = Guid.NewGuid(), UserId = user.Id, Title = "Bands", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(conversation);
        await repo.AddMessageAsync(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id, Role = ChatMessageRoles.User, Content = "Q1", Ordinal = 1, CreatedAt = DateTime.UtcNow });
        await repo.AddMessageAsync(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id, Role = ChatMessageRoles.Assistant, Content = "A1", Ordinal = 2, CreatedAt = DateTime.UtcNow });

        // Duplicate ordinal within a conversation is rejected by the unique index.
        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddMessageAsync(new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = ChatMessageRoles.User,
            Content = "dup",
            Ordinal = 2,
            CreatedAt = DateTime.UtcNow
        }));
        db.ChangeTracker.Clear();

        await using var db2 = _db.NewContext();
        var repo2 = new ChatConversationRepository(db2);
        var loaded = await repo2.GetWithMessagesAsync(conversation.Id);
        Assert.NotNull(loaded);
        Assert.Equal(new[] { "Q1", "A1" }, loaded!.Messages.Select(m => m.Content).ToArray());
        Assert.Equal(3, await repo2.NextOrdinalAsync(conversation.Id));

        var others = await repo2.ListForUserAsync(Guid.NewGuid(), 10);
        Assert.Empty(others);
    }
}
