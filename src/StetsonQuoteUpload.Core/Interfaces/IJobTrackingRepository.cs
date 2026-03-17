using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IJobTrackingRepository
{
    Task<Guid> CreateAsync(JobTracking job, CancellationToken ct = default);
    Task<JobTracking?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, string status, int itemsProcessed, int errors, CancellationToken ct = default);
    Task CompleteAsync(Guid id, string status, CancellationToken ct = default);
}
