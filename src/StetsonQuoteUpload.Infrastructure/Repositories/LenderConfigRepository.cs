using Microsoft.EntityFrameworkCore;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;
using StetsonQuoteUpload.Infrastructure.Data;

namespace StetsonQuoteUpload.Infrastructure.Repositories;

public class LenderConfigRepository : ILenderConfigRepository
{
    private readonly AppDbContext _db;

    public LenderConfigRepository(AppDbContext db) => _db = db;

    public Task<LenderConfiguration?> GetByNameAsync(string name, CancellationToken ct = default)
        => _db.LenderConfigurations.FirstOrDefaultAsync(l => l.Name == name, ct);
}
