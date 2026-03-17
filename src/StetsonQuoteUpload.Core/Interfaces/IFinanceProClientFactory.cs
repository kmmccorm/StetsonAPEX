using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IFinanceProClientFactory
{
    Task<IFinanceProClient> CreateAsync(LenderConfiguration config, CancellationToken ct = default);
}
