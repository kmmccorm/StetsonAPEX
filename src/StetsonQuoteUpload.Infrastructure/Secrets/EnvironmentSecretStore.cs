using StetsonQuoteUpload.Core.Interfaces;

namespace StetsonQuoteUpload.Infrastructure.Secrets;

/// <summary>
/// Reads secrets from environment variables — for local development only.
/// In production, replace with AzureKeyVaultSecretStore.
/// </summary>
public class EnvironmentSecretStore : ISecretStore
{
    public Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        var value = Environment.GetEnvironmentVariable(secretName)
            ?? throw new InvalidOperationException($"Secret '{secretName}' not found in environment variables");
        return Task.FromResult(value);
    }
}
