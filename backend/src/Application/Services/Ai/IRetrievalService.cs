using TaxNavi.Domain.Models;

namespace TaxNavi.Application.Services;

/// <summary>Embeds a question and returns the best supporting passages within the context budget.</summary>
public interface IRetrievalService
{
    Task<List<RetrievedChunk>> RetrieveAsync(string query, CancellationToken cancellationToken = default);
}
