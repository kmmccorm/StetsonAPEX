using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using StetsonQuoteUpload.Core.Interfaces;
using StetsonQuoteUpload.Core.Models;

namespace StetsonQuoteUpload.Infrastructure.FinancePro;

public class FinanceProClientFactory : IFinanceProClientFactory
{
    private readonly ISecretStore _secrets;
    private readonly ILoggerFactory _loggerFactory;

    public FinanceProClientFactory(ISecretStore secrets, ILoggerFactory loggerFactory)
    {
        _secrets = secrets;
        _loggerFactory = loggerFactory;
    }

    public async Task<IFinanceProClient> CreateAsync(LenderConfiguration config, CancellationToken ct = default)
    {
        var password = await _secrets.GetSecretAsync(config.PasswordSecretName!, ct);
        var importerKey = await _secrets.GetSecretAsync(config.ImporterKeySecretName!, ct);

        var credentials = new FinanceProCredentials
        {
            Username = config.Username ?? string.Empty,
            Password = password,
            ImporterKey = importerKey,
            EndpointUrl = $"{config.EndpointUrl}/webservices/QuoteService.asmx"
        };

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

        var handler = new PolicyHttpMessageHandler(retryPolicy)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        return new FinanceProSoapClient(
            httpClient,
            credentials,
            _loggerFactory.CreateLogger<FinanceProSoapClient>());
    }
}
