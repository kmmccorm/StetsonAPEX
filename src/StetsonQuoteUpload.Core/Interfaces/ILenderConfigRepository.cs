using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface ILenderConfigRepository
{
    Task<LenderConfiguration?> GetByNameAsync(string name, CancellationToken ct = default);
}
