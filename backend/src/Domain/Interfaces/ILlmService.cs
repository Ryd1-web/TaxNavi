using TaxNavi.Domain.Models;

namespace TaxNavi.Domain.Interfaces;

/// <summary>Provider-agnostic chat completion. The implementation decides the actual model.</summary>
public interface ILlmService
{
    bool IsConfigured { get; }
    string ModelName { get; }

    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}
