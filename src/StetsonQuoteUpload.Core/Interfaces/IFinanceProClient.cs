using StetsonQuoteUpload.Core.FinancePro;

namespace StetsonQuoteUpload.Core.Interfaces;

public interface IFinanceProClient
{
    Task<FpResults> GetQuoteByIdAsync(int quoteId, CancellationToken ct = default);
    Task<string> GetQuoteAgreementUrlAsync(int quoteId, CancellationToken ct = default);
}
