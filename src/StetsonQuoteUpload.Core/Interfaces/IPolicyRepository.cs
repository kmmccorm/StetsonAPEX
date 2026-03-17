using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IPolicyRepository
{
    Task MergeRangeAsync(IEnumerable<Policy> policies, CancellationToken ct = default);
}
