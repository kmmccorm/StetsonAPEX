using Microsoft.EntityFrameworkCore;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;
using StetsonQuoteUpload.Infrastructure.Data;

namespace StetsonQuoteUpload.Infrastructure.Repositories;

public class JobTrackingRepository : IJobTrackingRepository
{
    private readonly AppDbContext _db;

    public JobTrackingRepository(AppDbContext db) => _db = db;

    public async Task<Guid> CreateAsync(JobTracking job, CancellationToken ct = default)
    {
        _db.JobTrackings.Add(job);
        await _db.SaveChangesAsync(ct);
        return job.Id;
    }

    public Task<JobTracking?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.JobTrackings.FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task UpdateStatusAsync(Guid id, string status, int itemsProcessed, int errors, CancellationToken ct = default)
    {
        await _db.JobTrackings
            .Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, status)
                .SetProperty(j => j.ItemsProcessed, itemsProcessed)
                .SetProperty(j => j.NumberOfErrors, errors),
            ct);
    }

    public async Task CompleteAsync(Guid id, string status, CancellationToken ct = default)
    {
        await _db.JobTrackings
            .Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, status)
                .SetProperty(j => j.CompletedDate, DateTime.UtcNow),
            ct);
    }
}
