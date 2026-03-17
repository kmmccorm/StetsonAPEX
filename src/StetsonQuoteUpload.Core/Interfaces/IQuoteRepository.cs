using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IQuoteRepository
{
    Task<Quote?> GetByQuoteIdAsync(string quoteId, CancellationToken ct = default);
    Task<List<Quote>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
    Task UpsertRangeAsync(IEnumerable<Quote> quotes, CancellationToken ct = default);
    Task UpdateAsync(Quote quote, CancellationToken ct = default);
    Task UpdateErrorAsync(int id, string code, string description, string message, string severity, CancellationToken ct = default);
    Task<List<Quote>> GetQuotesForJobAsync(Guid jobId, CancellationToken ct = default);
    Task AssociateWithJobAsync(IEnumerable<int> quoteIds, Guid jobId, CancellationToken ct = default);
}
