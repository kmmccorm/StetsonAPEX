using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IPubSubConfigRepository
{
    Task<PubSubConfiguration?> GetFirstAsync(CancellationToken ct = default);
}
