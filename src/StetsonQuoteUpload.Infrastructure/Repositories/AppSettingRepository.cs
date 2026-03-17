using Microsoft.EntityFrameworkCore;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Infrastructure.Data;

namespace StetsonQuoteUpload.Infrastructure.Repositories;

public class AppSettingRepository : IAppSettingRepository
{
    private readonly AppDbContext _db;

    public AppSettingRepository(AppDbContext db) => _db = db;

    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        return setting?.Value;
    }

    public async Task<int> GetIntValueAsync(string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var value = await GetValueAsync(key, ct);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }
}
