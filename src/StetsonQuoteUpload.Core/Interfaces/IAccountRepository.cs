using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByAGTCodeAsync(string agtCode, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetIdsByAGTCodesAsync(IEnumerable<string> agtCodes, CancellationToken ct = default);
}
