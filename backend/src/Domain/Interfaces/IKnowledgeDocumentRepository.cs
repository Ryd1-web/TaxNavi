using TaxNavi.Domain.Entities;

namespace TaxNavi.Domain.Interfaces;

public interface IKnowledgeDocumentRepository
{
    Task<KnowledgeDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KnowledgeDocument?> FindByContentHashAsync(string contentHash, CancellationToken cancellationToken = default);
    Task<List<KnowledgeDocument>> ListAsync(CancellationToken cancellationToken = default);
    Task<int> NextVersionAsync(string title, CancellationToken cancellationToken = default);
    Task AddAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark every other Indexed document with the same title as Superseded (set-based, no PDF bytes loaded).
    /// Returns the number of documents superseded.
    /// </summary>
    Task<int> SupersedeOtherIndexedVersionsAsync(string title, Guid currentId, CancellationToken cancellationToken = default);
}
