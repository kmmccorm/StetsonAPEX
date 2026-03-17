namespace StetsonQuoteUpload.Core.Interfaces;

public interface ISecretStore
{
    Task<string> GetSecretAsync(string secretName, CancellationToken ct = default);
}
