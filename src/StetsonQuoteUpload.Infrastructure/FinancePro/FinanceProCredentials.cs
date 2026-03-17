namespace StetsonQuoteUpload.Infrastructure.FinancePro;

public class FinanceProCredentials
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ImporterKey { get; init; } = string.Empty;
    public string EndpointUrl { get; init; } = string.Empty;
}
