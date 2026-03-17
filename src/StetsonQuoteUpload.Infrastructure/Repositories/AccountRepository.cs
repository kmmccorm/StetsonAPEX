using Microsoft.EntityFrameworkCore;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;
using StetsonQuoteUpload.Infrastructure.Data;

namespace StetsonQuoteUpload.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _db;

    public AccountRepository(AppDbContext db) => _db = db;

    public Task<Account?> GetByAGTCodeAsync(string agtCode, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.AGTCode == agtCode, ct);

    public async Task<Dictionary<string, int>> GetIdsByAGTCodesAsync(IEnumerable<string> agtCodes, CancellationToken ct = default)
    {
        var codes = agtCodes.ToList();
        return await _db.Accounts
            .Where(a => a.AGTCode != null && codes.Contains(a.AGTCode))
            .ToDictionaryAsync(a => a.AGTCode!, a => a.Id, ct);
    }
}
