using StetsonQuoteUpload.Core.Interfaces;

namespace StetsonQuoteUpload.Infrastructure.Secrets;

/// <summary>
/// Azure Key Vault secret store.
/// Requires the Azure.Security.KeyVault.Secrets and Azure.Identity NuGet packages.
/// Configure the vault URI in appsettings.json under "KeyVault:Uri".
/// </summary>
public class AzureKeyVaultSecretStore : ISecretStore
{
    // Inject SecretClient from Azure.Security.KeyVault.Secrets when wiring up production.
    // This stub illustrates the integration point — add Azure.Security.KeyVault.Secrets package
    // and replace the body with: return (await _client.GetSecretAsync(secretName, cancellationToken: ct)).Value.Value;

    public Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
        => throw new NotImplementedException(
            "AzureKeyVaultSecretStore requires the Azure.Security.KeyVault.Secrets package. " +
            "Add it and implement this method with SecretClient.GetSecretAsync(secretName).");
}
