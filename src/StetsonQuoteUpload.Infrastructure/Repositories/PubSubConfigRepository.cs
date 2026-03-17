using Microsoft.EntityFrameworkCore;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;
using StetsonQuoteUpload.Infrastructure.Data;

namespace StetsonQuoteUpload.Infrastructure.Repositories;

public class PubSubConfigRepository : IPubSubConfigRepository
{
    private readonly AppDbContext _db;

    public PubSubConfigRepository(AppDbContext db) => _db = db;

    public Task<PubSubConfiguration?> GetFirstAsync(CancellationToken ct = default)
        => _db.PubSubConfigurations.FirstOrDefaultAsync(ct);
}
