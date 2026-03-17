using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;
using StetsonQuoteUpload.Infrastructure.Data;

namespace StetsonQuoteUpload.Infrastructure.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<QuoteRepository> _logger;

    public QuoteRepository(AppDbContext db, ILogger<QuoteRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<Quote?> GetByQuoteIdAsync(string quoteId, CancellationToken ct = default)
        => _db.Quotes.FirstOrDefaultAsync(q => q.QuoteId == quoteId, ct);

    public Task<List<Quote>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return _db.Quotes.Where(q => idList.Contains(q.Id)).ToListAsync(ct);
    }

    public Task<List<Quote>> GetQuotesForJobAsync(Guid jobId, CancellationToken ct = default)
        => _db.Quotes.Where(q => q.JobId == jobId).ToListAsync(ct);

    public async Task UpsertRangeAsync(IEnumerable<Quote> quotes, CancellationToken ct = default)
    {
        foreach (var quote in quotes)
        {
            var existing = await _db.Quotes
                .FirstOrDefaultAsync(q => q.QuoteId == quote.QuoteId, ct);

            if (existing == null)
            {
                _db.Quotes.Add(quote);
            }
            else
            {
                // Update placeholder fields only
                existing.Name = quote.Name;
                existing.StageName = quote.StageName;
                existing.AGTNumber = quote.AGTNumber;
                existing.QuotingForCode = quote.QuotingForCode;
                existing.QuotingForAltCode = quote.QuotingForAltCode;
                existing.AccountId = quote.AccountId;
                existing.CloseDate = quote.CloseDate;
                existing.APIPartnerID = quote.APIPartnerID;
                existing.LastModifiedDate = DateTime.UtcNow;
                // Copy Id back to the input quote for association
                quote.Id = existing.Id;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task AssociateWithJobAsync(IEnumerable<int> quoteIds, Guid jobId, CancellationToken ct = default)
    {
        var idList = quoteIds.ToList();
        if (idList.Count == 0) return;

        await _db.Quotes
            .Where(q => idList.Contains(q.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.JobId, jobId), ct);
    }

    public async Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        var existing = await _db.Quotes.FindAsync(new object[] { quote.Id }, ct)
            ?? throw new InvalidOperationException($"Quote {quote.Id} not found");

        _db.Entry(existing).CurrentValues.SetValues(quote);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateErrorAsync(int id, string code, string description, string message, string severity, CancellationToken ct = default)
    {
        await _db.Quotes
            .Where(q => q.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.IntegrationError, true)
                .SetProperty(q => q.ErrorCode, code)
                .SetProperty(q => q.ErrorDescription, description)
                .SetProperty(q => q.ErrorMessage, message)
                .SetProperty(q => q.ErrorSeverity, severity)
                .SetProperty(q => q.LastModifiedDate, DateTime.UtcNow),
            ct);
    }
}
